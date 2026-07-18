using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using Newtonsoft.Json;

namespace XFBIN_LIB.XFBIN {
    public class PAGE {
        public string PageName;

        public CHUNK_TABLE ChunkTable = new CHUNK_TABLE();

        private ObservableCollection<CHUNK_MAP> _chunkMappings = new ObservableCollection<CHUNK_MAP>();
        [JsonProperty("Chunk Maps")]
        public ObservableCollection<CHUNK_MAP> ChunkMappings {
            get { return _chunkMappings; }
            set {
                _chunkMappings = value;
            }
        }

        private ObservableCollection<EXTRA_CHUNK_MAP_INDICES> _extraMappings = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
        [JsonProperty("Chunk References")]
        public ObservableCollection<EXTRA_CHUNK_MAP_INDICES> ExtraMappings {
            get { return _extraMappings; }
            set {
                _extraMappings = value;
            }
        }
        private ObservableCollection<CHUNK> _chunks = new ObservableCollection<CHUNK>();
        [JsonProperty("Chunks")]
        public ObservableCollection<CHUNK> Chunks {
            get { return _chunks; }
            set {
                _chunks = value;
            }
        }
    }

    public class CHUNK {
        public UInt32 Size;
        public UInt32 ChunkMapIndex { get; set; }
        public UInt16 Version { get; set; }
        public UInt16 VersionAttribute { get; set; }
        public byte[] ChunkData;
    }
    public class READ_CHUNK {
        public string file_name { get; set; }
        public UInt16 Version { get; set; }
        public UInt16 VersionAttribute { get; set; }
        public string ChunkMapName { get; set; }
        public string ChunkTypeName { get; set; }
        public string ChunkPathName { get; set; }
    }

    public class READ_PAGE {
        public ObservableCollection<READ_CHUNK_MAP> ChunkMappings = new ObservableCollection<READ_CHUNK_MAP>();

        public ObservableCollection<READ_EXTRA_CHUNK_MAP_INDICES> ExtraMappings = new ObservableCollection<READ_EXTRA_CHUNK_MAP_INDICES>();

        public ObservableCollection<READ_CHUNK> Chunks = new ObservableCollection<READ_CHUNK>();

    }


}
