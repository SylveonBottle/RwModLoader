﻿using static Rwml.IO.RwmodIO;

namespace Rwml.IO;

// RWMOD file format: https://gist.github.com/Dual-Iron/ecc9c366d33adcb4d7f319d2c18d9062
sealed class RwmodHeader
{
    private const ushort CurrentVersion = 2;

    [Flags]
    public enum FileFlags : byte
    {
        RdbEntry = 0x1,
        AudbEntry = 0x2,
        Hidden = 0x4
    }

    public RwmodHeader(FileFlags flags, SemVer? version, string modName, string modOwner, string homepage)
    {
        Flags = flags;
        Version = version;
        Name = modName;
        Owner = modOwner;
        Homepage = homepage;
    }

    public readonly FileFlags Flags;
    public readonly SemVer? Version;
    public readonly string Name;
    public readonly string Owner;
    public readonly string Homepage;

    public void Write(Stream s)
    {
        s.Write(new byte[] { 0x52, 0x57, 0x4d, 0x4f, 0x44 }, 0, 5);
        WriteUInt16(s, CurrentVersion);

        s.WriteByte((byte)Flags);
        WriteStringFull(s, Version.HasValue ? Version.Value.ToString() : "");
        WriteStringFull(s, Name);
        WriteStringFull(s, Owner);
        WriteStringFull(s, Homepage);
    }

    public static Result<RwmodHeader, string> Read(Stream s)
    {
        byte[] b = new byte[5];

        s.Read(b, 0, 5);

        // TODO in a future version (probably 1.0), remove `b[0] != 0x5`
        // allowing 0x5 was a bug, but removing it would break backwards compatibility, so it must stay

        if (b[0] != 0x5 && b[0] != 0x52 || b[1] != 0x57 || b[2] != 0x4d || b[3] != 0x4f || b[4] != 0x44) {
            return "not a rwmod file";
        }

        int rwmodVersion = ReadUInt16(ref b, s);

        return rwmodVersion switch {
            CurrentVersion or 1 => Read(rwmodVersion, b, s),
            0 => "older version; delete the BepInEx folder and reinstall Realm",
            _ => "newer version; upgrade Realm"
        };
    }

    static Result<RwmodHeader, string> Read(int rwmodVersion, byte[] b, Stream s)
    {
        try {
            var flags = (FileFlags)s.ReadByte();

            SemVer? ver;

            string verStr = ReadStringFull(ref b, s);

            if (rwmodVersion > 1 && verStr.Length == 0) {
                ver = null;
            }
            else if (SemVer.Parse(verStr) is SemVer version) {
                ver = version;
            }
            else {
                return "invalid semantic version";
            }

            var modName = ReadStringFull(ref b, s);
            var modOwner = ReadStringFull(ref b, s);
            var homepage = ReadStringFull(ref b, s);

            return new RwmodHeader(flags, ver, modName, modOwner, homepage);
        }
        catch {
            return "corrupt file";
        }
    }
}
