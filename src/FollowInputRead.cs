namespace EquinoxCompanion;
// C++ bool is returned in one byte. A marshalled C# bool defaults to a four-byte
// BOOL and can turn unrelated false input results into true from upper bits.
internal delegate byte FollowInputRead(nint input,int id);
