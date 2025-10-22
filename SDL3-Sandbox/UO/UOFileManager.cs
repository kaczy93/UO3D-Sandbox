// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.IO;
using System.Diagnostics;
using SDL3_Sandbox.UO;

namespace ClassicUO.Assets
{
    public sealed class UOFileManager : IDisposable
    {

        public UOFileManager(ClientVersion clientVersion, string uoPath)
        {
            Version = clientVersion;
            BasePath = uoPath;

            IsUOPInstallation = true;

            Arts = new ArtLoader(this);
            TileData = new TileDataLoader(this);
            Hues = new HuesLoader(this);
            Texmaps = new TexmapsLoader(this);
        }

        public ClientVersion Version { get; }
        public string BasePath { get; }
        public bool IsUOPInstallation { get; private set; }

        public ArtLoader Arts { get; }
        public TileDataLoader TileData { get; }
        public HuesLoader Hues { get; }
        public TexmapsLoader Texmaps { get; }


        public void Dispose()
        {
            Arts.Dispose();
            TileData.Dispose();
            Hues.Dispose();
            Texmaps.Dispose();
        }

        public string GetUOFilePath(string file)
        {
            var uoFilePath = Path.Combine(BasePath, file);

            //If the file with the given name doesn't exist, check for it with alternative casing if not on windows
            if (File.Exists(uoFilePath))
            {
                FileInfo finfo = new FileInfo(uoFilePath);
                var dir = Path.GetFullPath(finfo.DirectoryName ?? BasePath);

                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir);
                    var matches = 0;

                    foreach (var f in files)
                    {
                        if (string.Equals(f, uoFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            matches++;
                            uoFilePath = f;
                        }
                    }

                    if (matches > 1)
                    {
                        Console.WriteLine($"Multiple files with ambiguous case found for {file}, using {Path.GetFileName(uoFilePath)}. Check your data directory for duplicate files.");
                    }
                }
            }

            return uoFilePath;
        }

        public void Load()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            
            Arts.Load();
            TileData.Load();
            Hues.Load();
            Texmaps.Load();
            
            ReadArtDefFile();
            
            Console.WriteLine($"Files loaded in: {stopwatch.ElapsedMilliseconds} ms!");
            stopwatch.Stop();
        }

        private void ReadArtDefFile()
        {
            string pathdef = GetUOFilePath("art.def");

            if (!File.Exists(pathdef))
            {
                return;
            }

            using (var reader = new DefReader(pathdef, 1))
            {
                while (reader.Next())
                {
                    int index = reader.ReadInt();

                    if (index < 0 || index >= ArtLoader.MAX_LAND_DATA_INDEX_COUNT + TileData.StaticData.Length)
                    {
                        continue;
                    }

                    int[] group = reader.ReadGroup();

                    if (group == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < group.Length; i++)
                    {
                        int checkIndex = group[i];

                        if (checkIndex < 0 || checkIndex >= ArtLoader.MAX_LAND_DATA_INDEX_COUNT + TileData.StaticData.Length)
                        {
                            continue;
                        }

                        if (index < Arts.File.Entries.Length && checkIndex < Arts.File.Entries.Length)
                        {
                            ref UOFileIndex currentEntry = ref Arts.File.GetValidRefEntry(index);
                            ref UOFileIndex checkEntry = ref Arts.File.GetValidRefEntry(checkIndex);

                            if (currentEntry.Equals(UOFileIndex.Invalid) && !checkEntry.Equals(UOFileIndex.Invalid))
                            {
                                Arts.File.Entries[index] = Arts.File.Entries[checkIndex];
                            }
                        }

                        if (index < ArtLoader.MAX_LAND_DATA_INDEX_COUNT &&
                            checkIndex < ArtLoader.MAX_LAND_DATA_INDEX_COUNT &&
                            checkIndex < TileData.LandData.Length &&
                            index < TileData.LandData.Length &&
                            !TileData.LandData[checkIndex].Equals(default) &&
                            TileData.LandData[index].Equals(default))
                        {
                            TileData.LandData[index] = TileData.LandData[checkIndex];

                            break;
                        }

                        if (index >= ArtLoader.MAX_LAND_DATA_INDEX_COUNT && checkIndex >= ArtLoader.MAX_LAND_DATA_INDEX_COUNT &&
                            index < TileData.StaticData.Length && checkIndex < TileData.StaticData.Length &&
                            TileData.StaticData[index].Equals(default) && !TileData.StaticData[checkIndex].Equals(default))
                        {
                            TileData.StaticData[index] = TileData.StaticData[checkIndex];

                            break;
                        }
                    }
                }
            }
        }
    }
}
