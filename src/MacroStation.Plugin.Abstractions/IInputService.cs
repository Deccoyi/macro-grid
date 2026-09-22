namespace MacroStation.Plugin.Abstractions;

/// <summary>Simulates keyboard input on the host machine.</summary>
public interface IInputService
{
    void SendKeyCombo(KeyCombo combo);

    void TypeText(string text);
}
