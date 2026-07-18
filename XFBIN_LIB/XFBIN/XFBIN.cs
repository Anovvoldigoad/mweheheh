using System.Collections.ObjectModel;
using System.Reflection.PortableExecutable;
using System.Text;

namespace XFBIN_LIB.XFBIN
{
    public class XFBIN
    {
        public char[] MAGIC { get; set; }
        public UInt32 FileID { get; set; }
        public UInt32 ChunkTableSize { get; set; }
        public UInt32 MinPageSize { get; set; }
        public UInt16 FileVersion { get; set; }
        public UInt16 FileVersionAttribute { get; set; }

        private CHUNK_TABLE _chunkTable = new CHUNK_TABLE();
        public CHUNK_TABLE ChunkTable {
            get { return _chunkTable; }
            set {
                _chunkTable = value;
            }
        }
        private ObservableCollection<PAGE> _pages = new ObservableCollection<PAGE>();
        public ObservableCollection<PAGE> Pages {
            get { return _pages; }
            set {
                _pages = value;
            }
        }

    }
   
}