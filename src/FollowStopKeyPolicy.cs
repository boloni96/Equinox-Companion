namespace EquinoxCompanion;
public static class FollowStopKeyPolicy
{
    // Only a configured movement letter/arrow/numpad key without modifiers.
    // Never substitute Escape, Enter, Space, mouse buttons or a guessed default.
    public static bool Allowed(int key,int modifiers)=>modifiers==0&&(key is >=0x41 and <=0x5A or >=0x25 and <=0x28 or >=0x60 and <=0x69);
}
