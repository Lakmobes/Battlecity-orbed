using BattleCity.Client.Input;
using BattleCity.Shared.Network.Packets;

using Microsoft.Xna.Framework.Input;

namespace BattleCity.Client.Chat;

public sealed class InGameChatInput
{
    private readonly LoginTextInput _textInput = new(maxLength: ChatPacketLimits.MaxMessageLength);
    private KeyboardState _previousKeyboard;
    private bool _isActive;
    private bool _openedThisFrame;

    public bool IsActive => _isActive;

    public string Draft => _textInput.Text;

    public void Close()
    {
        _isActive = false;
        _textInput.SetText(string.Empty);
    }

    /// <summary>Snapshot the current keyboard so a held Enter from a prior scene does not open chat.</summary>
    public void Reset()
    {
        Close();
        _previousKeyboard = Keyboard.GetState();
        _openedThisFrame = false;
    }

    public ChatInputUpdate Update(KeyboardState keyboard)
    {
        _openedThisFrame = false;

        if (!_isActive)
        {
            if (WasEnterPressed(keyboard))
            {
                _isActive = true;
                _textInput.SetText(string.Empty);
                _openedThisFrame = true;
                _previousKeyboard = keyboard;
                return ChatInputUpdate.Editing;
            }

            _previousKeyboard = keyboard;
            return ChatInputUpdate.None;
        }

        if (WasPressed(keyboard, Keys.Escape))
        {
            Close();
            _previousKeyboard = keyboard;
            return ChatInputUpdate.Cancelled;
        }

        if (!_openedThisFrame && WasEnterPressed(keyboard))
        {
            var message = _textInput.Text.Trim();
            Close();
            _previousKeyboard = keyboard;
            return string.IsNullOrWhiteSpace(message)
                ? ChatInputUpdate.Cancelled
                : ChatInputUpdate.SubmitMessage(message);
        }

        _textInput.Update();
        _previousKeyboard = keyboard;
        return ChatInputUpdate.Editing;
    }

    private bool WasEnterPressed(KeyboardState keyboard) =>
        WasPressed(keyboard, Keys.Enter);

    private bool WasPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
}

public readonly struct ChatInputUpdate
{
    private ChatInputUpdate(bool isActive, bool submitted, string message)
    {
        IsActive = isActive;
        Submitted = submitted;
        Message = message;
    }

    public bool IsActive { get; }

    public bool Submitted { get; }

    public string Message { get; }

    public static ChatInputUpdate None => default;

    public static ChatInputUpdate Editing => new(isActive: true, submitted: false, message: string.Empty);

    public static ChatInputUpdate Cancelled => new(isActive: false, submitted: false, message: string.Empty);

    public static ChatInputUpdate SubmitMessage(string message) =>
        new(isActive: false, submitted: true, message);
}
