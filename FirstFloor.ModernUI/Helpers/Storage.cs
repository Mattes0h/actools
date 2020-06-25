using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public class Storage : NotifyPropertyChanged, IStorage, IDisposable {
        [CanBeNull]
        public static string TemporaryBackupsDirectory { get; set; }

        private readonly Dictionary<string, string> _storage;
        private readonly string _filename;
        private readonly string _encryptionKey;
        private readonly bool _disableCompression;
        private readonly bool _useDeflate;

        public Storage(string filename = null, string encryptionKey = null, bool disableCompression =
#if DEBUG
                true,
#else
                false,
#endif
                bool useDeflate = false) {
            _storage = new Dictionary<string, string>();
            _filename = filename;
            _encryptionKey = encryptionKey;
            _disableCompression = disableCompression;
            _useDeflate = useDeflate;

            Load();
            Exit += OnExit;
        }

        private void OnExit(object sender, EventArgs e) {
            if (_dirty) {
                Save();
            }
        }

        public void Dispose() {
            if (_dirty) {
                Save();
            }

            Exit -= OnExit;
        }

        public int Count {
            get {
                lock (_storage) {
                    return _storage.Count;
                }
            }
        }

        private TimeSpan? _previousSaveTime;

        public TimeSpan? PreviousSaveTime {
            get => _previousSaveTime;
            set => Apply(value, ref _previousSaveTime);
        }

        private const byte DeflateFlag = 0;
        private const byte LzfFlag = 11;

        private string DecodeBytes(byte[] bytes) {
            if (bytes[0] == LzfFlag) {
                return Encoding.UTF8.GetString(Lzf.Decompress(bytes, 1, bytes.Length - 1));
            }

            var deflateMode = bytes[0] == DeflateFlag;
            if (!deflateMode && !bytes.Any(x => x < 0x20 && x != '\t' && x != '\n' && x != '\r')) {
                return Encoding.UTF8.GetString(bytes);
            }

            using (var inputStream = new MemoryStream(bytes)) {
                if (deflateMode) {
                    inputStream.Seek(1, SeekOrigin.Begin);
                }

                using (var gzip = new DeflateStream(inputStream, CompressionMode.Decompress)) {
                    using (var reader = new StreamReader(gzip, Encoding.UTF8)) {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private byte[] EncodeBytes(string s) {
            var bytes = Encoding.UTF8.GetBytes(s);
            if (_disableCompression) return bytes;

            using (var output = new MemoryStream()) {
                output.WriteByte(DeflateFlag);

                if (_useDeflate) {
                    using (var gzip = new DeflateStream(output, CompressionLevel.Fastest)) {
                        gzip.Write(bytes, 0, bytes.Length);
                    }
                } else {
                    Lzf.Compress(bytes, bytes.Length, output);
                }

                return output.ToArray();
            }
        }

        public static string EncodeBase64([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var plainTextBytes = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string DecodeBase64([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var base64EncodedBytes = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        [NotNull, Pure]
        public static string EncodeList([CanBeNull, ItemCanBeNull] params string[] list) {
            return EncodeList((IEnumerable<string>)list);
        }

        [NotNull, Pure]
        public static string EncodeList([CanBeNull, ItemCanBeNull] IEnumerable<string> list) {
            return list == null ? "" : string.Join("\n", list.Where(x => !string.IsNullOrEmpty(x)).Select(Encode));
        }

        [NotNull, ItemNotNull, Pure]
        public static IEnumerable<string> DecodeList([CanBeNull] string encoded) {
            return encoded?.Split('\n').Where(x => !string.IsNullOrEmpty(x)).Select(Decode) ?? new string[0];
        }

        public static string Encode([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var result = new StringBuilder(s.Length + 5);
            foreach (var c in s) {
                switch (c) {
                    case '\\':
                        result.Append(@"\\");
                        break;

                    case '\n':
                        result.Append(@"\n");
                        break;

                    case '\t':
                        result.Append(@"\t");
                        break;

                    case '\b':
                    case '\r':
                        break;

                    default:
                        result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        public static string Decode([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var result = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c != '\\') {
                    result.Append(c);
                    continue;
                }

                if (++i >= s.Length) {
                    break;
                }

                switch (s[i]) {
                    case '\\':
                        result.Append(@"\");
                        break;

                    case 'n':
                        result.Append("\n");
                        break;

                    case 't':
                        result.Append("\t");
                        break;
                }
            }

            return result.ToString();
        }

        private void Load() {
            var w = Stopwatch.StartNew();
            if (_filename == null || !File.Exists(_filename)) {
                lock (_storage) {
                    _storage.Clear();
                }
                return;
            }

            try {
                var splitted = DecodeBytes(File.ReadAllBytes(_filename))
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                Load(int.Parse(splitted[0].Split(new[] { "version:" }, StringSplitOptions.None)[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture),
                        splitted.Skip(1));
            } catch (Exception e) {
                Logging.Warning(e);
                lock (_storage) {
                    _storage.Clear();
                }
            }

            OnPropertyChanged(nameof(Count));
            if (w.Elapsed.TotalMilliseconds > 2) {
                Logging.Write($"{Path.GetFileName(_filename)}: {w.Elapsed.TotalMilliseconds:F2} ms");
            }
        }

        private const int ActualVersion = 2;

        private void Load(int version, IEnumerable<string> data) {
            lock (_storage) {
                _storage.Clear();
                switch (version) {
                    case 2:
                        foreach (var split in data
                                .Select(line => line.Split(new[] { '\t' }, 2))
                                .Where(split => split.Length == 2)) {
                            _storage[Decode(split[0])] = Decode(split[1]);
                        }
                        break;

                    case 1:
                        foreach (var split in data
                                .Select(line => line.Split(new[] { '\t' }, 2))
                                .Where(split => split.Length == 2)) {
                            _storage[split[0]] = DecodeBase64(split[1]);
                        }
                        break;

                    default:
                        throw new InvalidDataException("Invalid version: " + version);
                }
            }
        }

        public string GetData() {
            lock (_storage) {
                return "version: " + ActualVersion + "\n" + string.Join("\n", from x in _storage
                                                                              where x.Key != null && x.Value != null
                                                                              select Encode(x.Key) + '\t' + Encode(x.Value));
            }
        }

        private static string EnsureUnique([NotNull] string filename, int startFrom = 1) {
            if (!File.Exists(filename) && !Directory.Exists(filename)) {
                return filename;
            }

            var ext = Path.GetExtension(filename);
            var start = filename.Substring(0, filename.Length - ext.Length);

            for (var i = startFrom; i < 99999; i++) {
                var result = $"{start}-{i}{ext}";
                if (File.Exists(result) || Directory.Exists(result)) continue;
                return result;
            }

            throw new Exception("Can’t find unique filename");
        }

        private void SaveData(string data) {
            try {
                var bytes = Encoding.UTF8.GetBytes(data);
                var newFilename = EnsureUnique(_filename);

                if (_disableCompression) {
                    File.WriteAllBytes(newFilename, bytes);
                } else {
                    using (var output = new FileStream(newFilename, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                        if (_useDeflate) {
                            output.WriteByte(DeflateFlag);
                            using (var gzip = new DeflateStream(output, CompressionLevel.Fastest)) {
                                gzip.Write(bytes, 0, bytes.Length);
                            }
                        } else {
                            output.WriteByte(LzfFlag);
                            Lzf.Compress(bytes, bytes.Length, output);
                        }
                    }
                }

                if (newFilename == _filename) return;

                if (TemporaryBackupsDirectory != null) {
                    var backupFilename = Path.Combine(TemporaryBackupsDirectory, Path.GetFileName(_filename));
                    try {
                        if (File.Exists(backupFilename)) {
                            File.Delete(backupFilename);
                        }
                        File.Move(_filename, backupFilename);
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                } else {
                    File.Delete(_filename);
                }

                try {
                    if (File.Exists(_filename)) {
                        File.Delete(_filename);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }

                File.Move(newFilename, _filename);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void Save() {
            if (_filename == null) return;
            SaveData(GetData());
        }

        public void ForceSave() {
            SaveData(GetData());
        }

        private async Task SaveAsync() {
            if (_filename == null) return;
            var sw = Stopwatch.StartNew();
            var data = GetData();
            await Task.Run(() => SaveData(data));
            PreviousSaveTime = sw.Elapsed;
        }

        public static event EventHandler Exit;

        public static void SaveBeforeExit() {
            Exit?.Invoke(null, EventArgs.Empty);
        }

        private bool _dirty;

        private async void Dirty() {
            if (_dirty) return;

            try {
                _dirty = true;
                await Task.Delay(5000);
                await SaveAsync();
            } finally {
                _dirty = false;
            }
        }

        [ContractAnnotation("defaultValue:null => canbenull; defaultValue:notnull => notnull")]
        public T Get<T>(string key, T defaultValue = default) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                return _storage.TryGetValue(key, out var value) ? value.As<T>() : defaultValue;
            }
        }

        public IEnumerable<string> GetStringList(string key, IEnumerable<string> defaultValue = null) {
            string value;

            lock (_storage) {
                if (!_storage.TryGetValue(key, out value) || string.IsNullOrEmpty(value)) {
                    return defaultValue ?? new string[] { };
                }
            }

            var s = value.Split('\n');
            for (var i = s.Length - 1; i >= 0; i--) {
                s[i] = Decode(s[i]);
            }
            return s;
        }

        [Pure]
        public bool Contains([LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                return _storage.ContainsKey(key);
            }
        }

        public void Set([LocalizationRequired(false)] string key, object value) {
            var v = value.AsShort<string>();
            lock (_storage) {
                var exists = _storage.TryGetValue(key, out var previous);
                if (exists && previous == v) return;

                _storage[key] = v;
                Dirty();

                if (exists) {
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public void SetStringList(string key, IEnumerable<string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Set(key, string.Join("\n", value.Select(Encode)));
        }

        /* I know that this is not a proper protection or anything, but I just don’t want to save some
            stuff plain-texted */
        private const string Something = "encisfinedontworry";

        [Pure, CanBeNull]
        public string Decrypt([NotNull, LocalizationRequired(false)] string key, [NotNull, LocalizationRequired(false)] string value) {
            if (_encryptionKey == null) return value;
            var result = StringCipher.Decrypt(value, key + _encryptionKey);
            return result == null ? null : result.EndsWith(Something) ? result.Substring(0, result.Length - Something.Length) : null;
        }

        public T GetEncrypted<T>([LocalizationRequired(false)] string key, T defaultValue = default) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (_encryptionKey == null || !Contains(key)) return defaultValue;
            var result = StringCipher.Decrypt(Get<string>(key), key + _encryptionKey);
            return result?.EndsWith(Something) == true ? result.Substring(0, result.Length - Something.Length).As(defaultValue) : defaultValue;
        }

        public void SetEncrypted([LocalizationRequired(false)] string key, object value) {
            if (_encryptionKey == null) {
                Logging.Warning($"Encryption key is not set to save {key}");
                return;
            }

            var encrypted = StringCipher.Encrypt(value.AsShort<string>() + Something, key + _encryptionKey);
            if (encrypted == null) {
                Remove(key);
            } else {
                Set(key, encrypted);
            }
        }

        public bool Remove([LocalizationRequired(false)] string key) {
            lock (_storage) {
                if (_storage.ContainsKey(key)) {
                    _storage.Remove(key);
                    Dirty();
                    OnPropertyChanged(nameof(Count));
                    return true;
                }
            }

            return false;
        }

        public void CleanUp(Func<string, bool> predicate) {
            lock (_storage) {
                var keys = _storage.Keys.ToList();
                foreach (var key in keys.Where(predicate)) {
                    _storage.Remove(key);
                }
            }

            Dirty();
            ActionExtension.InvokeInMainThread(() => { OnPropertyChanged(nameof(Count)); });
        }

        public void CopyFrom(Storage source) {
            lock (_storage) {
                _storage.Clear();
                foreach (var pair in source) {
                    _storage[pair.Key] = pair.Value;
                }
            }

            Dirty();
            ActionExtension.InvokeInMainThread(() => { OnPropertyChanged(nameof(Count)); });
        }

        public IEnumerable<string> Keys {
            get {
                lock (_storage) {
                    return _storage.Keys;
                }
            }
        }

        public void Clear() {
            lock (_storage) {
                _storage.Clear();
            }
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
            lock (_storage) {
                return _storage.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}