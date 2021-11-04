﻿namespace Realm.ModLoading;

readonly struct RwmodFileHeader
{
    public readonly string FilePath;
    public readonly RwmodHeader Header;

    public RwmodFileHeader(string filePath, RwmodHeader header)
    {
        FilePath = filePath;
        Header = header;
    }

    public static ICollection<RwmodFileHeader> GetRwmodHeaders()
    {
        List<RwmodFileHeader> ret = new();

        foreach (var file in RwmodFile.GetRwmodFilePaths()) {
            using Stream s = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (RwmodHeader.Read(s).MatchSuccess(out var header, out _))
                ret.Add(new(file, header));
        }

        return ret;
    }
}
