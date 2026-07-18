using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using XFBIN_LIB;
using XFBIN_LIB.XFBIN;

namespace XFBIN_LIB.Converter
{
    public class ReadPageConverter : JsonConverter<READ_PAGE>
    {
        public override READ_PAGE Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject token");


            READ_PAGE new_page = new READ_PAGE();
            READ_CHUNK_MAP chunk_map = new READ_CHUNK_MAP();
            READ_EXTRA_CHUNK_MAP_INDICES extra_map = new READ_EXTRA_CHUNK_MAP_INDICES();
            READ_CHUNK chunk = new READ_CHUNK();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new_page;

                if (reader.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException("Expected PropertyName token");

                var propName = reader.GetString();
                reader.Read();

                switch (propName)
                {
                    case "Chunk Maps":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                //Console.WriteLine(reader.GetString());
                                switch (reader.GetString())
                                {
                                    case "Name":
                                        chunk_map = new READ_CHUNK_MAP();
                                        reader.Read();
                                        chunk_map.ChunkName = reader.GetString();
                                        break;
                                    case "Type":
                                        reader.Read();
                                        chunk_map.TypeName = reader.GetString();
                                        break;
                                    case "Path":
                                        reader.Read();
                                        chunk_map.FilePathName = reader.GetString();
                                        new_page.ChunkMappings.Add(chunk_map);
                                        break;
                                }
                            }
                        }
                        break;
                    case "Chunk References":
                        int offset = 0;

                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {



                            if (reader.TokenType == JsonTokenType.String)
                            {
                                switch (offset)
                                {
                                    case 0:
                                        extra_map = new READ_EXTRA_CHUNK_MAP_INDICES();
                                        extra_map.ExtraChunkMapName = reader.GetString();
                                        offset++;
                                        break;
                                    case 1:
                                        extra_map.ChunkMapName = reader.GetString();
                                        offset++;
                                        break;
                                    case 2:
                                        extra_map.ChunkTypeName = reader.GetString();
                                        offset++;
                                        break;
                                    case 3:
                                        extra_map.ChunkPathName = reader.GetString();
                                        new_page.ExtraMappings.Add(extra_map);
                                        offset = 0;
                                        break;
                                }
                            }

                        }
                        break;
                    case "Chunks":
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                switch (reader.GetString())
                                {
                                    case "File Name":
                                        chunk = new READ_CHUNK();
                                        reader.Read();
                                        chunk.file_name = reader.GetString();
                                        break;
                                    case "Version":
                                        reader.Read();
                                        chunk.Version = reader.GetUInt16();
                                        break;
                                    case "Version Attribute":
                                        reader.Read();
                                        chunk.VersionAttribute = reader.GetUInt16();
                                        break;
                                    case "Chunk":
                                        reader.Read();
                                        break;
                                    case "Name":
                                        reader.Read();
                                        chunk.ChunkMapName = reader.GetString();
                                        break;
                                    case "Type":
                                        reader.Read();
                                        chunk.ChunkTypeName = reader.GetString();
                                        break;
                                    case "Path":
                                        reader.Read();
                                        chunk.ChunkPathName = reader.GetString();
                                        new_page.Chunks.Add(chunk);
                                        break;
                                }
                            }

                        }
                        break;
                }
            }

            throw new JsonException("Expected EndObject token");
        }
        public override void Write(Utf8JsonWriter writer, READ_PAGE value, JsonSerializerOptions options)
        {

        }
    }

    public class PageConverter : JsonConverter<PAGE>
    {
        public static Dictionary<string, string> file_format = new Dictionary<string, string>(){
            { "nuccChunkUnknown", ".unk"},
            { "nuccChunkClump", ".clump"},
            { "nuccChunkAnm", ".anm"},
            { "nuccChunkTexture", ".nut"},
            { "nuccChunkAnmStrm", ".anmstrm"},
            { "nuccChunkAnmStrmFrame", ".anmstrmframe"},
            { "nuccChunkModel", ".model"},
            { "nuccChunkModelHit", ".modelhit"},
            { "nuccChunkMaterial", ".material"},
            { "nuccChunkNub", ".nub"},
            { "nuccChunkCoord", ".coord"},
            { "nuccChunkDynamics", ".dynamics"},
            { "nuccChunkTrail", ".trail"},
            { "nuccChunkBillboard", ".billboard"},
            { "nuccChunkBinary", ".bin"},
            { "nuccChunkParticle", ".particle"},
            { "nuccChunkPrimitiveVertex", ".primver"},
            { "nuccChunkModelPrimitiveBatch", ".modprimbatch"},
            { "nuccChunkCamera", ".camera"},
            { "nuccChunkSprite", ".sprite"},
            { "nuccChunkLightDirc", ".light_dirc"},
            { "nuccChunkLightPoint", ".light_point"},
            { "nuccChunkAmbient", ".ambient"},
            { "nuccChunkSpriteAnm", ".spriteanm"},
            { "nuccChunkFont", ".font"},
            { "nuccChunkMorphPrimitive", ".morphprim"},
            { "nuccChunkLayerSet", ".layerset"},
            { "nuccChunkModelVertex", ".modelvert"},
            { "nuccChunkLightSet", ".light_set"},
            { "nuccChunkSprite2", ".sprite2"},
            { "nuccChunkSprite2Anm", ".sprite2anm"},
        };
        public override PAGE Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return null;
        }
        public override void Write(Utf8JsonWriter writer, PAGE value, JsonSerializerOptions options)
        {
            //Start
            writer.WriteStartObject();
            writer.WriteStartArray("Chunk Maps");
            foreach (CHUNK_MAP chunk_map in value.ChunkMappings)
            {
                writer.WriteStartObject();
                writer.WriteString("Name", value.ChunkTable.ChunkNames[(int)chunk_map.ChunkNameIndex].ChunkName);
                writer.WriteString("Type", value.ChunkTable.ChunkTypes[(int)chunk_map.ChunkTypeIndex].ChunkTypeName);
                writer.WriteString("Path", value.ChunkTable.FilePaths[(int)chunk_map.FilePathIndex].FilePathName);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("Chunk References");
            foreach (EXTRA_CHUNK_MAP_INDICES extra_map in value.ExtraMappings)
            {
                writer.WriteStartObject();
                writer.WriteString("Name", value.ChunkTable.ChunkNames[(int)extra_map.ChunkNameIndex].ChunkName);
                writer.WriteStartObject("Chunk");
                writer.WriteString("Name", value.ChunkTable.ChunkNames[(int)value.ChunkTable.ChunkMaps[(int)extra_map.ChunkMapIndex].ChunkNameIndex].ChunkName);
                writer.WriteString("Type", value.ChunkTable.ChunkTypes[(int)value.ChunkTable.ChunkMaps[(int)extra_map.ChunkMapIndex].ChunkTypeIndex].ChunkTypeName);
                writer.WriteString("Path", value.ChunkTable.FilePaths[(int)value.ChunkTable.ChunkMaps[(int)extra_map.ChunkMapIndex].FilePathIndex].FilePathName);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("Chunks");

            foreach (CHUNK chunk in value.Chunks)
            {
                if (value.ChunkTable.FilePaths[(int)value.ChunkTable.ChunkMaps[(int)value.ChunkTable.ChunkMapIndices[(int)chunk.ChunkMapIndex].ChunkMapIndex].FilePathIndex].FilePathName == "")
                    continue;
                string format = ".bin";
                string type = value.ChunkTable.ChunkTypes[(int)value.ChunkTable.ChunkMaps[(int)value.ChunkTable.ChunkMapIndices[(int)chunk.ChunkMapIndex].ChunkMapIndex].ChunkTypeIndex].ChunkTypeName;
                if (file_format.ContainsKey(type))
                    format = file_format[type];

                writer.WriteStartObject();
                writer.WriteString("File Name", value.ChunkTable.ChunkNames[(int)value.ChunkTable.ChunkMaps[(int)value.ChunkTable.ChunkMapIndices[(int)chunk.ChunkMapIndex].ChunkMapIndex].ChunkNameIndex].ChunkName + format);
                writer.WriteNumber("Version", chunk.Version);
                writer.WriteNumber("Version Attribute", chunk.VersionAttribute);
                writer.WriteStartObject("Chunk");
                writer.WriteString("Name", value.ChunkTable.ChunkNames[(int)value.ChunkTable.ChunkMaps[(int)value.ChunkTable.ChunkMapIndices[(int)chunk.ChunkMapIndex].ChunkMapIndex].ChunkNameIndex].ChunkName);
                writer.WriteString("Type", type);
                writer.WriteString("Path", value.ChunkTable.FilePaths[(int)value.ChunkTable.ChunkMaps[(int)value.ChunkTable.ChunkMapIndices[(int)chunk.ChunkMapIndex].ChunkMapIndex].FilePathIndex].FilePathName);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            //End
            writer.WriteEndObject();
        }
    }
}