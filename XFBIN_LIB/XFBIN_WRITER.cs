using MiscUtil.Conversion;
using MiscUtil.IO;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using XFBIN_LIB.Converter;
using XFBIN_LIB.XFBIN;

namespace XFBIN_LIB
{
    public class XFBIN_WRITER
    {
        public static XFBIN.XFBIN ReadDirectoryXFBIN(string path)
        {
            ObservableCollection<READ_PAGE> page_list = new ObservableCollection<READ_PAGE>();
            XFBIN.XFBIN xfbin_file = new XFBIN.XFBIN();
            ObservableCollection<CHUNK_NAME> chunk_name_list = new ObservableCollection<CHUNK_NAME>();
            ObservableCollection<CHUNK_TYPE> chunk_type_list = new ObservableCollection<CHUNK_TYPE>();
            ObservableCollection<FILE_PATH> chunk_path_list = new ObservableCollection<FILE_PATH>();
            ObservableCollection<CHUNK_MAP> chunk_map_list = new ObservableCollection<CHUNK_MAP>();
            ObservableCollection<EXTRA_CHUNK_MAP_INDICES> extra_chunk_map_indices = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
            ObservableCollection<CHUNK_MAP_INDICES> chunk_map_indices = new ObservableCollection<CHUNK_MAP_INDICES>();
            ObservableCollection<PAGE> new_page_list = new ObservableCollection<PAGE>();
            CHUNK_TABLE chunk_table = new CHUNK_TABLE();
            List<string> json_list = Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories).ToList();

            //read page json
            foreach (string file in Directory.EnumerateFiles(path, "*.json", SearchOption.AllDirectories))
            {
                using (FileStream fs = new FileStream(file, FileMode.Open))
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    options.Converters.Add(new ReadPageConverter());
                    READ_PAGE page = JsonSerializer.Deserialize<READ_PAGE>(fs, options);
                    page_list.Add(page);
                }
            }

            //merge all values to prevent repeated values
            ObservableCollection<string> read_chunk_name_list = new ObservableCollection<string>();
            ObservableCollection<string> read_chunk_type_list = new ObservableCollection<string>();
            ObservableCollection<string> read_chunk_path_list = new ObservableCollection<string>();
            foreach (READ_PAGE page in page_list)
            {
                foreach (READ_CHUNK_MAP read_chunk_map in page.ChunkMappings)
                {
                    if (!read_chunk_name_list.Contains(read_chunk_map.ChunkName))
                    {
                        read_chunk_name_list.Add(read_chunk_map.ChunkName);
                    }
                    if (!read_chunk_type_list.Contains(read_chunk_map.TypeName))
                    {
                        read_chunk_type_list.Add(read_chunk_map.TypeName);
                    }
                    if (!read_chunk_path_list.Contains(read_chunk_map.FilePathName))
                    {
                        read_chunk_path_list.Add(read_chunk_map.FilePathName);
                    }
                    CHUNK_MAP _chunk_map = new CHUNK_MAP();
                }
                foreach (READ_EXTRA_CHUNK_MAP_INDICES read_extra_chunk_map in page.ExtraMappings)
                {
                    if (!read_chunk_name_list.Contains(read_extra_chunk_map.ExtraChunkMapName))
                    {
                        read_chunk_name_list.Add(read_extra_chunk_map.ExtraChunkMapName);
                    }
                    if (!read_chunk_name_list.Contains(read_extra_chunk_map.ChunkMapName))
                    {
                        read_chunk_name_list.Add(read_extra_chunk_map.ChunkMapName);
                    }
                    if (!read_chunk_type_list.Contains(read_extra_chunk_map.ChunkTypeName))
                    {
                        read_chunk_type_list.Add(read_extra_chunk_map.ChunkTypeName);
                    }
                    if (!read_chunk_path_list.Contains(read_extra_chunk_map.ChunkPathName))
                    {
                        read_chunk_path_list.Add(read_extra_chunk_map.ChunkPathName);
                    }
                }
                foreach (READ_CHUNK read_chunk in page.Chunks)
                {
                    if (!read_chunk_name_list.Contains(read_chunk.ChunkMapName))
                    {
                        read_chunk_name_list.Add(read_chunk.ChunkMapName);
                    }
                    if (!read_chunk_type_list.Contains(read_chunk.ChunkTypeName))
                    {
                        read_chunk_type_list.Add(read_chunk.ChunkTypeName);
                    }
                    if (!read_chunk_path_list.Contains(read_chunk.ChunkPathName))
                    {
                        read_chunk_path_list.Add(read_chunk.ChunkPathName);
                    }
                }
            }
            // add string values to lists
            // Chunk Names
            foreach (string name in read_chunk_name_list)
            {
                CHUNK_NAME chunk_name = new CHUNK_NAME();
                chunk_name.ChunkName = name;
                chunk_name_list.Add(chunk_name);
            }
            //Chunk Types
            foreach (string name in read_chunk_type_list)
            {
                CHUNK_TYPE chunk_type = new CHUNK_TYPE();
                chunk_type.ChunkTypeName = name;
                chunk_type_list.Add(chunk_type);
            }
            //Chunk Paths
            foreach (string name in read_chunk_path_list)
            {
                FILE_PATH chunk_path = new FILE_PATH();
                chunk_path.FilePathName = name;
                chunk_path_list.Add(chunk_path);
            }

            //global chunk_map list from all jsons
            List<READ_CHUNK_MAP> pages_chunk_map_list = new List<READ_CHUNK_MAP>();
            foreach (READ_PAGE page in page_list)
            {
                foreach (READ_CHUNK_MAP _ch_map in page.ChunkMappings)
                {
                    pages_chunk_map_list.Add(_ch_map);
                }
            }
            List<READ_CHUNK_MAP> merged_chunk_map_list = pages_chunk_map_list.GroupBy(car => (car.TypeName, car.ChunkName, car.FilePathName)).Select(g => g.First()).ToList();
            //create chunk map list
            foreach (READ_CHUNK_MAP _ch_map in merged_chunk_map_list)
            {
                CHUNK_MAP new_chunk_map = new CHUNK_MAP();
                new_chunk_map.ChunkNameIndex = (uint)read_chunk_name_list.IndexOf(_ch_map.ChunkName);
                new_chunk_map.ChunkTypeIndex = (uint)read_chunk_type_list.IndexOf(_ch_map.TypeName);
                new_chunk_map.FilePathIndex = (uint)read_chunk_path_list.IndexOf(_ch_map.FilePathName);
                chunk_map_list.Add(new_chunk_map);
            }

            foreach (READ_PAGE page in page_list)
            {
                //create Extra Mapping list 
                foreach (READ_EXTRA_CHUNK_MAP_INDICES _ch_map in page.ExtraMappings)
                {
                    EXTRA_CHUNK_MAP_INDICES new_extra_map_indices = new EXTRA_CHUNK_MAP_INDICES();
                    new_extra_map_indices.ChunkNameIndex = (uint)read_chunk_name_list.IndexOf(_ch_map.ExtraChunkMapName);
                    int chunk_map_index = merged_chunk_map_list.FindIndex(r => (r.ChunkName == _ch_map.ChunkMapName && r.TypeName == _ch_map.ChunkTypeName && r.FilePathName == _ch_map.ChunkPathName));
                    new_extra_map_indices.ChunkMapIndex = (uint)chunk_map_index;
                    extra_chunk_map_indices.Add(new_extra_map_indices);
                }
                //create chunk map indices list
                foreach (READ_CHUNK_MAP _ch_map in page.ChunkMappings)
                {
                    CHUNK_MAP_INDICES new_chunk_map_index = new CHUNK_MAP_INDICES();
                    int chunk_map_index = merged_chunk_map_list.FindIndex(r => (r.ChunkName == _ch_map.ChunkName && r.TypeName == _ch_map.TypeName && r.FilePathName == _ch_map.FilePathName));
                    new_chunk_map_index.ChunkMapIndex = (uint)chunk_map_index;
                    chunk_map_indices.Add(new_chunk_map_index);
                }
            }

            //create chunk table
            chunk_table.ChunkTypeCount = (uint)chunk_type_list.Count();
            Enumerable.Range(0, chunk_type_list.Count()).ToList().ForEach(x => chunk_table.ChunkTypeSize += (uint)chunk_type_list[x].ChunkTypeName.Length + 1);
            chunk_table.FilePathCount = (uint)chunk_path_list.Count();
            Enumerable.Range(0, chunk_path_list.Count()).ToList().ForEach(x => chunk_table.FilePathSize += (uint)chunk_path_list[x].FilePathName.Length + 1);
            chunk_table.ChunkNameCount = (uint)chunk_name_list.Count();
            Enumerable.Range(0, chunk_name_list.Count()).ToList().ForEach(x => chunk_table.ChunkNameSize += (uint)chunk_name_list[x].ChunkName.Length + 1);
            chunk_table.ChunkMapCount = (uint)chunk_map_list.Count();
            chunk_table.ChunkMapSize = (uint)chunk_map_list.Count() * 0xC;
            chunk_table.ChunkMapIndicesCount = (uint)chunk_map_indices.Count();
            chunk_table.ExtraIndicesCount = (uint)extra_chunk_map_indices.Count();

            chunk_table.ChunkTypes = chunk_type_list;
            chunk_table.ChunkNames = chunk_name_list;
            chunk_table.FilePaths = chunk_path_list;
            chunk_table.ChunkMaps = chunk_map_list;
            chunk_table.ExtraMappings = extra_chunk_map_indices;
            chunk_table.ChunkMapIndices = chunk_map_indices;

            //write pages
            int page_size = 0;
            int page_index = 0;
            int extramap_size = 0;
            int page_offset = 0;
            int extramap_offset = 0;
            foreach (READ_PAGE page in page_list)
            {
                string json_path = json_list[page_index];
                string page_path = Path.GetDirectoryName(json_path);
                PAGE new_page = new PAGE();
                //make chunk map list for page
                List<READ_CHUNK_MAP> temp_chunk_map_for_page = new List<READ_CHUNK_MAP>();
                ObservableCollection<CHUNK_MAP> temp_chunk_map_for_table = new ObservableCollection<CHUNK_MAP>();
                for (int i = 0 + page_offset; i < page.ChunkMappings.Count() + page_offset; i++)
                {
                    READ_CHUNK_MAP temp_chunk_map = new READ_CHUNK_MAP();
                    CHUNK_MAP temp2_chunk_map = new CHUNK_MAP();
                    temp_chunk_map = merged_chunk_map_list[(int)chunk_map_indices[i].ChunkMapIndex];
                    temp2_chunk_map = chunk_table.ChunkMaps[(int)chunk_map_indices[i].ChunkMapIndex];
                    temp_chunk_map_for_page.Add(temp_chunk_map);
                    temp_chunk_map_for_table.Add(temp2_chunk_map);
                }
                //make extra map list for page
                ObservableCollection<EXTRA_CHUNK_MAP_INDICES> temp_extra_map_for_table = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
                for (int i = 0 + extramap_offset; i < page.ExtraMappings.Count() + extramap_offset; i++)
                {
                    EXTRA_CHUNK_MAP_INDICES temp_extra_map = new EXTRA_CHUNK_MAP_INDICES();
                    temp_extra_map = chunk_table.ExtraMappings[i];
                    temp_extra_map_for_table.Add(temp_extra_map);
                }

                //Add Null Chunk
                CHUNK null_chunk = new CHUNK();
                null_chunk.Version = 121;
                null_chunk.VersionAttribute = 0;
                null_chunk.Size = 0;
                null_chunk.ChunkData = new byte[0];
                null_chunk.ChunkMapIndex = (uint)temp_chunk_map_for_page.FindIndex(r => (r.ChunkName == "" && r.TypeName == "nuccChunkNull" && r.FilePathName == ""));
                new_page.Chunks.Add(null_chunk);

                foreach (READ_CHUNK chunk in page.Chunks)
                {
                    CHUNK new_chunk = new CHUNK();
                    new_chunk.Version = chunk.Version;
                    new_chunk.VersionAttribute = chunk.VersionAttribute;
                    new_chunk.ChunkData = File.ReadAllBytes(page_path + "\\" + chunk.file_name);
                    new_chunk.Size = (uint)new_chunk.ChunkData.Length;
                    new_chunk.ChunkMapIndex = (uint)temp_chunk_map_for_page.FindIndex(r => (r.ChunkName == chunk.ChunkMapName && r.TypeName == chunk.ChunkTypeName && r.FilePathName == chunk.ChunkPathName));
                    new_page.Chunks.Add(new_chunk);
                }
                page_size = page.ChunkMappings.Count();
                extramap_size = page.ExtraMappings.Count();
                page_offset += page_size;
                extramap_offset += extramap_size;
                //Add Page Chunk
                CHUNK page_chunk = new CHUNK();
                page_chunk.Version = 121;
                page_chunk.VersionAttribute = 0;
                page_chunk.Size = 8;

                byte[] page_value = new byte[8];
                byte[] page_size_byte = BitConverter.GetBytes(page_size);
                byte[] extramap_size_byte = BitConverter.GetBytes(extramap_size);
                Array.Reverse(page_size_byte);
                Array.Reverse(extramap_size_byte);
                Array.Copy(page_size_byte, 0, page_value, 0, 4);
                Array.Copy(extramap_size_byte, 0, page_value, 4, 4);
                page_chunk.ChunkData = page_value;
                page_chunk.ChunkMapIndex = (uint)temp_chunk_map_for_page.FindIndex(r => (r.ChunkName == "Page0" && r.TypeName == "nuccChunkPage" && r.FilePathName == ""));
                new_page.Chunks.Add(page_chunk);

                //Add variables in page for reading inside of tools
                new_page.ChunkMappings = temp_chunk_map_for_table;
                new_page.ExtraMappings = temp_extra_map_for_table;
                new_page.ChunkTable = chunk_table;

                new_page_list.Add(new_page);
                page_index++;
            }

            xfbin_file.MAGIC = "NUCC".ToCharArray();
            xfbin_file.FileVersion = 121;
            xfbin_file.FileID = 121;
            xfbin_file.ChunkTableSize = 0x28 + chunk_table.ChunkNameSize + chunk_table.ChunkTypeSize + chunk_table.FilePathSize + chunk_table.ChunkMapSize + (chunk_table.ChunkMapIndicesCount * 4);
            xfbin_file.ChunkTableSize += 4 - (xfbin_file.ChunkTableSize % 4);
            xfbin_file.ChunkTable = chunk_table;
            xfbin_file.MinPageSize = 3;
            xfbin_file.Pages = new_page_list;
            return xfbin_file;
        }

        public static void RepackXFBIN(string path)
        {
            XFBIN.XFBIN xfbin_file = XFBIN_WRITER.ReadDirectoryXFBIN(path);
            string outputFile = Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path) + ".xfbin");
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }

            using (EndianBinaryWriter writer = new EndianBinaryWriter(EndianBitConverter.Big, File.Open(outputFile, FileMode.CreateNew)))
            {
                // Write XFBIN HEADER
                writer.Write(xfbin_file.MAGIC);
                writer.Write(xfbin_file.FileID);
                writer.Write(new byte[8]);
                writer.Write(xfbin_file.ChunkTableSize);
                writer.Write(xfbin_file.MinPageSize);
                writer.Write(xfbin_file.FileVersion);
                writer.Write(xfbin_file.FileVersionAttribute);
                writer.Write(xfbin_file.ChunkTable.ChunkTypeCount);
                writer.Write(xfbin_file.ChunkTable.ChunkTypeSize);
                writer.Write(xfbin_file.ChunkTable.FilePathCount);
                writer.Write(xfbin_file.ChunkTable.FilePathSize);
                writer.Write(xfbin_file.ChunkTable.ChunkNameCount);
                writer.Write(xfbin_file.ChunkTable.ChunkNameSize);
                writer.Write(xfbin_file.ChunkTable.ChunkMapCount);
                writer.Write(xfbin_file.ChunkTable.ChunkMapSize);
                writer.Write(xfbin_file.ChunkTable.ChunkMapIndicesCount);
                writer.Write(xfbin_file.ChunkTable.ExtraIndicesCount);

                // Write type list
                foreach (CHUNK_TYPE type in xfbin_file.ChunkTable.ChunkTypes)
                {
                    writer.Write(Encoding.ASCII.GetBytes(type.ChunkTypeName));
                    writer.Write(new byte[1]);
                }
                // Write path list
                foreach (FILE_PATH file_path in xfbin_file.ChunkTable.FilePaths)
                {
                    writer.Write(Encoding.ASCII.GetBytes(file_path.FilePathName));
                    writer.Write(new byte[1]);
                }
                // Write name list
                foreach (CHUNK_NAME chunk_name in xfbin_file.ChunkTable.ChunkNames)
                {
                    writer.Write(Encoding.ASCII.GetBytes(chunk_name.ChunkName));
                    writer.Write(new byte[1]);
                }

                // Skip bytes to align to 4-byte boundary
                long skip = 4 - (writer.BaseStream.Position % 4);
                if (skip == 4)
                    skip = 0;
                writer.Write(new byte[skip]);

                // Write chunk map list
                foreach (CHUNK_MAP chunk_map in xfbin_file.ChunkTable.ChunkMaps)
                {
                    writer.Write(chunk_map.ChunkTypeIndex);
                    writer.Write(chunk_map.FilePathIndex);
                    writer.Write(chunk_map.ChunkNameIndex);
                }
                // Write extra map list
                foreach (EXTRA_CHUNK_MAP_INDICES extra_map in xfbin_file.ChunkTable.ExtraMappings)
                {
                    writer.Write(extra_map.ChunkNameIndex);
                    writer.Write(extra_map.ChunkMapIndex);
                }
                // Write chunk map indices list
                foreach (CHUNK_MAP_INDICES chunk_map_indices in xfbin_file.ChunkTable.ChunkMapIndices)
                {
                    writer.Write(chunk_map_indices.ChunkMapIndex);
                }
                // Write pages
                foreach (PAGE page in xfbin_file.Pages)
                {
                    for (int i = 0; i < page.Chunks.Count(); i++)
                    {
                        writer.Write(page.Chunks[i].Size);
                        writer.Write(page.Chunks[i].ChunkMapIndex);
                        writer.Write(page.Chunks[i].Version);
                        writer.Write(page.Chunks[i].VersionAttribute);
                        writer.Write(page.Chunks[i].ChunkData);
                    }
                }
            }
        }



    }
}