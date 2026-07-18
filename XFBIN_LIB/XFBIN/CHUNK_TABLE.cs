using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XFBIN_LIB.XFBIN {
    public class CHUNK_TABLE {
        public UInt32 ChunkTypeCount { get; set; }
        public UInt32 ChunkTypeSize { get; set; }
        public UInt32 FilePathCount { get; set; }
        public UInt32 FilePathSize { get; set; }
        public UInt32 ChunkNameCount { get; set; }
        public UInt32 ChunkNameSize { get; set; }
        public UInt32 ChunkMapCount { get; set; }
        public UInt32 ChunkMapSize { get; set; }
        public UInt32 ChunkMapIndicesCount { get; set; }
        public UInt32 ExtraIndicesCount { get; set; }

        private ObservableCollection<CHUNK_TYPE> _chunkTypes = new ObservableCollection<CHUNK_TYPE>();
        public ObservableCollection<CHUNK_TYPE> ChunkTypes {
            get { return _chunkTypes; }
            set {
                _chunkTypes = value;
            }
        }
        private ObservableCollection<FILE_PATH> _filePaths = new ObservableCollection<FILE_PATH>();
        public ObservableCollection<FILE_PATH> FilePaths {
            get { return _filePaths; }
            set {
                _filePaths = value;
            }
        }
        private ObservableCollection<CHUNK_NAME> _chunkNames = new ObservableCollection<CHUNK_NAME>();
        public ObservableCollection<CHUNK_NAME> ChunkNames {
            get { return _chunkNames; }
            set {
                _chunkNames = value;
            }
        }
        private ObservableCollection<CHUNK_MAP> _chunkMaps = new ObservableCollection<CHUNK_MAP>();
        public ObservableCollection<CHUNK_MAP> ChunkMaps {
            get { return _chunkMaps; }
            set {
                _chunkMaps = value;
            }
        }
        private ObservableCollection<EXTRA_CHUNK_MAP_INDICES> _extraMappings = new ObservableCollection<EXTRA_CHUNK_MAP_INDICES>();
        public ObservableCollection<EXTRA_CHUNK_MAP_INDICES> ExtraMappings {
            get { return _extraMappings; }
            set {
                _extraMappings = value;
            }
        }
        private ObservableCollection<CHUNK_MAP_INDICES> _chunkMapIndices = new ObservableCollection<CHUNK_MAP_INDICES>();
        public ObservableCollection<CHUNK_MAP_INDICES> ChunkMapIndices {
            get { return _chunkMapIndices; }
            set {
                _chunkMapIndices = value;
            }
        }
    }
    public class CHUNK_TYPE {
        public string ChunkTypeName { get; set; }
    }
    public class FILE_PATH {
        public string FilePathName { get; set; }
    }
    public class CHUNK_NAME {
        public string ChunkName { get; set; }
    }
    public class CHUNK_MAP {
        public UInt32 ChunkTypeIndex { get; set; }
        public UInt32 FilePathIndex { get; set; }
        public UInt32 ChunkNameIndex { get; set; }
    }
    public class READ_CHUNK_MAP {
        public string TypeName { get; set; }
        public string FilePathName { get; set; }
        public string ChunkName { get; set; }
    }
    public class EXTRA_CHUNK_MAP_INDICES {
        public UInt32 ChunkNameIndex { get; set; }
        public UInt32 ChunkMapIndex { get; set; }
    }
    public class READ_EXTRA_CHUNK_MAP_INDICES {
        public string ExtraChunkMapName { get; set; }
        public string ChunkMapName { get; set; }
        public string ChunkTypeName { get; set; }
        public string ChunkPathName { get; set; }
    }
    public class CHUNK_MAP_INDICES {
        public UInt32 ChunkMapIndex { get; set; }
    }
}
