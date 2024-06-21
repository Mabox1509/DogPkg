//[INCLUDES]
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DogPkg
{
    public class Program
    {
        public static int Main(string[] _args)
        {
            Stopwatch _watch = new Stopwatch();
            _watch.Start();

            if (_args == null || _args.Length <= 0)
            {
                Console.WriteLine("Args empty - Correct format:\n<app_path> <assets_path> <out_path>");
                Console.Write("Press any key to continue");
                Console.ReadLine();

                return 1;
            }

            string[] _paths = Directory.GetFiles(_args[0], "", searchOption: SearchOption.AllDirectories);
            List<byte> _header_buffer = new List<byte>();
            List<byte> _data_buffer = new List<byte>();

            _header_buffer.AddRange(new byte[8]);
            uint _assets_count = 0;

            for (int i = 0; i < _paths.Length; i++)
            {
                string _ext = Path.GetExtension(_paths[i]);
                string _key = _paths[i].Replace(_args[0], "");
                _key = _key.Remove(0, 1);


                //LOAD AND HANDLE DATA
                byte[] _head_data = null;
                byte[] _file_data = null;

                byte _type = 0;

                switch (_ext)
                {
                    case ".png":
                        CodifyTexture(_paths[i], out _file_data, out _head_data);
                        _assets_count++;
                        _type = 0;
                        break;

                    case ".wav":
                        CodifyAudio(_paths[i], out _file_data, out _head_data);
                        _assets_count++;
                        _type = 1;
                        break;

                    case ".obj":
                        CodifyModel(_paths[i], out _file_data, out _head_data);
                        _assets_count++;
                        _type = 4;
                        break;

                    case ".meta":
                        Console.WriteLine($"{_paths[i]} HEADER FILE -- SKIP");
                        break;

                    default:
                        _file_data = File.ReadAllBytes(_paths[i]);
                        _assets_count++;

                        if (_ext == ".txt" || _ext == ".json" || _ext == ".csv")
                            _type = 2;
                        else
                            _type = 3;

                        break;
                }

                if (_file_data == null)
                    continue;

                _header_buffer.AddRange(Encoding.ASCII.GetBytes(_key));
                _header_buffer.Add(0x00);
                _header_buffer.Add(_type);

                _header_buffer.AddRange(BitConverter.GetBytes(_data_buffer.Count));
                _header_buffer.AddRange(BitConverter.GetBytes(_file_data.Length));

                _header_buffer.AddRange(BitConverter.GetBytes(_head_data != null ? _head_data.Length : 0));
                if (_head_data != null)
                    _header_buffer.AddRange(_head_data);




                _data_buffer.AddRange(_file_data);

                Console.WriteLine($"{_paths[i]} - Packed");
                if (_file_data.Length >= 0xFFFF)
                {
                    GC.Collect();
                    Thread.Sleep(10);
                }

            }
            byte[] _head_size_bytes = BitConverter.GetBytes(_header_buffer.Count);
            _header_buffer[0] = _head_size_bytes[0];
            _header_buffer[1] = _head_size_bytes[1];
            _header_buffer[2] = _head_size_bytes[2];
            _header_buffer[3] = _head_size_bytes[3];

            byte[] _entry_count_bytes = BitConverter.GetBytes(_assets_count);
            _header_buffer[4] = _entry_count_bytes[0];
            _header_buffer[5] = _entry_count_bytes[1];
            _header_buffer[6] = _entry_count_bytes[2];
            _header_buffer[7] = _entry_count_bytes[3];

            GC.Collect();
            List<byte> _final_buffer = new List<byte>();
            _final_buffer.AddRange(_header_buffer);
            _final_buffer.AddRange(_data_buffer);

            File.WriteAllBytes(Path.Combine(_args[1], "stream_assets"), _final_buffer.ToArray());

            _watch.Stop();
            Console.WriteLine($"Files packed in {_watch.ElapsedMilliseconds / 1000f}s");

            return 0;
        }

        public static byte[] CompressBuffer(byte[] data)
        {
            MemoryStream output = new MemoryStream();
            using (DeflateStream dstream = new DeflateStream(output, CompressionLevel.Optimal))
            {
                dstream.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        public static Dictionary<string, object> ReadMeta(string path)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();

            try
            {
                string[] lines = File.ReadAllLines(path);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                        continue;

                    string[] parts = line.Split(':');
                    if (parts.Length != 2)
                        throw new FormatException($"Invalid format in line: {line}");

                    string variable = parts[0].Trim();
                    string value = parts[1].Trim();

                    if (value.StartsWith("\"") && value.EndsWith("\""))
                    {
                        result[variable] = value.Trim('\"');
                    }
                    else if (float.TryParse(value, out float floatValue))
                    {
                        result[variable] = floatValue;
                    }
                    else
                    {
                        throw new FormatException($"Invalid value format in line: {line}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading metadata file: {ex.Message}");
                return null;
            }

            return result;
        }
        public static void LoadObj(string filePath, out List<Vector3> vertices, out List<int> triangles, out List<Vector2> uvs)
        {
            vertices = new List<Vector3>();
            triangles = new List<int>();
            uvs = new List<Vector2>();

            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (tokens.Length == 0) continue;

                    switch (tokens[0])
                    {
                        case "v":
                            if (tokens.Length >= 4)
                            {
                                vertices.Add(new Vector3(
                                    float.Parse(tokens[1]),
                                    float.Parse(tokens[2]),
                                    float.Parse(tokens[3])
                                ));
                            }
                            break;

                        case "vt":
                            if (tokens.Length >= 3)
                            {
                                uvs.Add(new Vector2(
                                    float.Parse(tokens[1]),
                                    float.Parse(tokens[2])
                                ));
                            }
                            break;

                        case "f":
                            if (tokens.Length >= 4)
                            {
                                for (int i = 1; i < tokens.Length - 2; i++)
                                {
                                    AddFace(tokens[1], tokens[i + 1], tokens[i + 2], triangles);
                                }
                            }
                            break;
                    }
                }
            }
        }
        private static void AddFace(string v1, string v2, string v3, List<int> triangles)
        {
            int vertIndex1 = int.Parse(v1.Split('/')[0]) - 1;
            int vertIndex2 = int.Parse(v2.Split('/')[0]) - 1;
            int vertIndex3 = int.Parse(v3.Split('/')[0]) - 1;

            triangles.Add(vertIndex1);
            triangles.Add(vertIndex2);
            triangles.Add(vertIndex3);
        }


        static void CodifyTexture(string _path, out byte[] _file, out byte[] _head)
        {
            Dictionary<string, object> _meta = null;

            if (File.Exists(_path + ".meta"))
                _meta = ReadMeta(_path + ".meta");


            //GENERATE META DATA
            byte _format = 0;
            bool _compress = true;

            int _frame_width = -1;
            int _frame_heigth = -1;

            float _x_pivot = 0;
            float _y_pivot = 0;

            bool _filter = false;

            if (_meta != null)
            {
                switch (_meta["color_format"])
                {
                    case "RGBA":
                        _format = 0;
                        break;

                    case "RGB":
                        _format = 1;
                        break;

                    case "IA":
                        _format = 2;
                        break;

                    case "I":
                        _format = 3;
                        break;
                }

                _compress = string.Compare((string)_meta["compress"], "true") == 0;
                _compress = string.Compare((string)_meta["filter"], "true") == 0;

                _frame_width = (int)(float)_meta["frame_width"];
                _frame_heigth = (int)(float)_meta["frame_heigth"];

                _x_pivot = (float)_meta["x_pivot"];
                _y_pivot = (float)_meta["y_pivot"];
            }
            Bitmap _bm = new Bitmap(_path);

            //FILE DATA
            int _bpp = 5 - (_format + 1);
            _file = new byte[(_bm.Width * _bm.Height) * _bpp];
            for (int _x = 0; _x < _bm.Width; _x++)
            {
                for (int _y = 0; _y < _bm.Height; _y++)
                {
                    int _index = ((_y * _bm.Width) + _x) * _bpp;
                    Color _pixel = _bm.GetPixel(_x, _y);

                    if (_format == 0)
                    {
                        _file[_index + 0] = _pixel.R;
                        _file[_index + 1] = _pixel.G;
                        _file[_index + 2] = _pixel.B;
                        _file[_index + 3] = _pixel.A;
                    }
                    else if (_format == 1)
                    {
                        _file[_index + 0] = _pixel.R;
                        _file[_index + 1] = _pixel.G;
                        _file[_index + 2] = _pixel.B;
                    }
                    else if (_format == 2)
                    {
                        float _i = (_pixel.R + _pixel.G + _pixel.B) / 3.0f;

                        _file[_index + 0] = (byte)_i;
                        _file[_index + 1] = _pixel.A;
                    }
                    else
                    {
                        float _i = (_pixel.R + _pixel.G + _pixel.B) / 3.0f;

                        _file[_index + 0] = (byte)_i;
                        _file[_index + 0] = _pixel.R;
                    }
                }
            }
            if (_frame_width < 0)
                _frame_width = _bm.Width;

            if (_frame_heigth < 0)
                _frame_heigth = _bm.Height;

            if (_compress)
            {
                _file = CompressBuffer(_file);
            }



            //HEADER DATA
            List<byte> _header = new List<byte>();
            _header.Add(_format);
            _header.Add((byte)(_compress ? 0xFF : 0x00));
            _header.Add((byte)(_filter ? 0xFF : 0x00));
            _header.AddRange(BitConverter.GetBytes(_bm.Width));
            _header.AddRange(BitConverter.GetBytes(_bm.Height));
            _header.AddRange(BitConverter.GetBytes(_x_pivot));
            _header.AddRange(BitConverter.GetBytes(_y_pivot));

            int _xcells = (int)((float)_bm.Width / _frame_width);
            int _ycells = (int)((float)_bm.Height / _frame_heigth);
            _header.AddRange(BitConverter.GetBytes(_xcells + _ycells));
            for (int _y = 0; _y < _ycells; _y++)
            {
                for (int _x = 0; _x < _xcells; _x++)
                {
                    _header.AddRange(BitConverter.GetBytes(_x * _frame_width));
                    _header.AddRange(BitConverter.GetBytes(_y * _frame_heigth));
                }
            }



            //END
            _head = _header.ToArray();
            _header = null;
            _bm = null;
            _meta = null;
        }
        static void CodifyAudio(string _path, out byte[] _file, out byte[] _head)
        {
            float[] _left;
            float[] _right;
            int _sapmple_ratio;

            AudioConverter.ConvertFileToWave(_path, out _left, out _right, out _sapmple_ratio);
            bool _stereo = _right != null && (_right[0] != 0 && _right[_right.Length - 1] != 0 && _right[_right.Length / 2] != 0);


            _file = new byte[_left.Length * (_stereo ? 4 : 2)];
            for (int i = 0; i < _left.Length; i++)
            {
                short _lv = (short)(_left[i] * short.MaxValue);
                byte[] _lv_bytes = BitConverter.GetBytes(_lv);

                // Write left channel data
                _file[i * 2] = _lv_bytes[0];
                _file[i * 2 + 1] = _lv_bytes[1];

                if (_stereo)
                {
                    short _rv = (short)(_right[i] * short.MaxValue);
                    byte[] _rv_bytes = BitConverter.GetBytes(_rv);

                    // Write right channel data
                    _file[_file.Length / 2 + i * 2] = _rv_bytes[0];
                    _file[_file.Length / 2 + i * 2 + 1] = _rv_bytes[1];
                }
            }

            _file = CompressBuffer(_file);


            //HEADER DATA
            List<byte> _header = new List<byte>();
            _header.AddRange(BitConverter.GetBytes(_left.Length));
            _header.AddRange(BitConverter.GetBytes(_sapmple_ratio));
            _header.Add((byte)(_stereo ? 0xFF : 0x00));

            _head = _header.ToArray();
        }
        static void CodifyModel(string _path, out byte[] _file, out byte[] _head)
        {
            //READ .obj
            List<Vector3> _verts;
            List<int> _tris;
            List<Vector2> _uvs;
            LoadObj(_path, out _verts, out _tris, out _uvs);

            //CODIFY
            List<byte> _buffer = new List<byte>();
            _buffer.AddRange(BitConverter.GetBytes(_verts.Count));
            if(_verts.Count > 0) 
            {
                for(int i = 0;i < _verts.Count; i++) 
                {
                    _buffer.AddRange(BitConverter.GetBytes(_verts[i].X));
                    _buffer.AddRange(BitConverter.GetBytes(_verts[i].Y));
                    _buffer.AddRange(BitConverter.GetBytes(_verts[i].Z));
                }
            }

            _buffer.AddRange(BitConverter.GetBytes(_tris.Count));
            if (_tris.Count > 0)
            {
                for (int i = 0; i < _tris.Count; i++)
                {
                    _buffer.AddRange(BitConverter.GetBytes(_tris[i]));
                }
            }

            _buffer.AddRange(BitConverter.GetBytes(_uvs.Count));
            if (_uvs.Count > 0)
            {
                for (int i = 0; i < _uvs.Count; i++)
                {
                    _buffer.AddRange(BitConverter.GetBytes(_uvs[i].X));
                    _buffer.AddRange(BitConverter.GetBytes(_uvs[i].Y));
                }
            }


            //END
            _file = _buffer.ToArray();
            _head = null;

            _buffer = null;
        }
    }
}