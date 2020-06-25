using System;
using System.ComponentModel;
using System.Diagnostics;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Dialogs {
    public class AsyncProgressBytesStopwatch {
        public long StartBytes;
        public Stopwatch Stopwatch = Stopwatch.StartNew();

        private double _averageSpeed;

        public string GetBytesProgressInTime(long processed, long total) {
            var elapsed = Stopwatch.Elapsed.TotalSeconds;
            var speed = (processed - StartBytes) / elapsed;
            if (speed < 30) {
                return "paused";
            }

            if (elapsed > 0.5d && processed - StartBytes > 128) {
                Stopwatch.Restart();
                StartBytes = processed;
            }

            _averageSpeed += (speed - _averageSpeed) * Math.Min(1d, elapsed * Math.Max(1d, _averageSpeed / (speed + 0.001) - 1));

            var displaySpeed = $"{((long)_averageSpeed).ToReadableSize(1)}/s";
            if (total == -1 || total < processed) return displaySpeed;

            var left = total - processed;
            var leftTime = TimeSpan.FromSeconds(left / _averageSpeed);
            return $"{displaySpeed}, {leftTime.ToReadableTime()} left";
        }
    }

    // TODO: SORT THIS SHIT OUT! ALL THAT 0.0001–0.9999 IS VERY IDIOTIC.
    public struct AsyncProgressEntry : INotifyPropertyChanged {
        public static readonly AsyncProgressEntry Indetermitate = new AsyncProgressEntry("", 0d);
        public static readonly AsyncProgressEntry Ready = new AsyncProgressEntry("", 1d);
        public static readonly AsyncProgressEntry Finished = new AsyncProgressEntry(null, null);

        public static AsyncProgressEntry FromStringIndetermitate(string message) {
            return new AsyncProgressEntry(message, 0d);
        }

        public bool IsIndeterminate => Equals(Progress, 0d);
        public bool IsReady => Equals(Progress, 1d);

        public AsyncProgressEntry(string message, double? progress) {
            Message = message;
            Progress = progress;
        }

        public AsyncProgressEntry(string message, int value, int total) {
            Message = message;
            const double x = 0.000001;
            Progress = (double)value / total * (1d - 2d * x) + x;
        }

        public string Message { get; }
        public double? Progress { get; }

        public static AsyncProgressEntry CreateDownloading(long receivedBytes, long totalBytes, [CanBeNull] AsyncProgressBytesStopwatch stopwatch) {
            var knownTotal = totalBytes > receivedBytes;

            string message;
            if (stopwatch != null) {
                message = (knownTotal
                        ? string.Format(UiStrings.Progress_Short_KnownTotal, receivedBytes.ToReadableSize(1), totalBytes.ToReadableSize(1))
                        : string.Format(UiStrings.Progress_Short, receivedBytes.ToReadableSize(1)))
                        + ", " + stopwatch.GetBytesProgressInTime(receivedBytes, totalBytes);
            } else {
                message = knownTotal
                        ? string.Format(UiStrings.Progress_Downloading_KnownTotal, receivedBytes.ToReadableSize(1), totalBytes.ToReadableSize(1))
                        : string.Format(UiStrings.Progress_Downloading, receivedBytes.ToReadableSize(1));
            }

            return knownTotal ? new AsyncProgressEntry(message, (double)receivedBytes / totalBytes) : new AsyncProgressEntry(message, null);
        }

        public static AsyncProgressEntry CreateUploading(long sentBytes, long totalBytes, [CanBeNull] AsyncProgressBytesStopwatch stopwatch) {
            var knownTotal = totalBytes > sentBytes;

            string message;
            if (stopwatch != null) {
                message = (knownTotal
                        ? string.Format(UiStrings.Progress_Short_KnownTotal, sentBytes.ToReadableSize(1), totalBytes.ToReadableSize(1))
                        : string.Format(UiStrings.Progress_Short, sentBytes.ToReadableSize(1)))
                        + ", " + stopwatch.GetBytesProgressInTime(sentBytes, totalBytes);
            } else {
                message = knownTotal
                        ? string.Format(UiStrings.Progress_Uploading_KnownTotal, sentBytes.ToReadableSize(1), totalBytes.ToReadableSize(1))
                        : string.Format(UiStrings.Progress_Uploading, sentBytes.ToReadableSize(1));
            }

            return knownTotal ? new AsyncProgressEntry(message, (double)sentBytes / totalBytes) : new AsyncProgressEntry(message, null);
        }

        public override string ToString() {
            return $@"{Message ?? @"<NULL>"} ({Progress * 100:F1}%)";
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged {
            add { }
            remove { }
        }

        public static IProgress<AsyncProgressEntry> Split(ref IProgress<AsyncProgressEntry> progress, double splitPoint) {
            var result = progress.Subrange(0.0001, splitPoint - 0.0001);
            progress = progress.Subrange(splitPoint + 0.0001, 0.9999);
            return result;
        }

        public static IProgress<AsyncProgressEntry> Split(ref IProgress<AsyncProgressEntry> progress, double splitPoint, string forceMessage,
                bool ignoreIndeterminate = true) {
            var result = progress.Subrange(0.0001, splitPoint - 0.0001, forceMessage, ignoreIndeterminate);
            progress = progress.Subrange(splitPoint + 0.0001, 0.9999);
            return result;
        }

        public bool Equals(AsyncProgressEntry other) {
            return string.Equals(Message, other.Message) && Progress.Equals(other.Progress);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is AsyncProgressEntry entry && Equals(entry);
        }

        public override int GetHashCode() {
            unchecked {
                return ((Message != null ? Message.GetHashCode() : 0) * 397) ^ Progress.GetHashCode();
            }
        }
    }

    public static class AsyncProgressEntryExtension {
        private class SubrangeProgress : IProgress<AsyncProgressEntry> {
            private readonly IProgress<AsyncProgressEntry> _baseProgress;
            private readonly double _from;
            private readonly double _range;
            private readonly string _forceMessage;
            private readonly bool _ignoreIndeterminate;

            public SubrangeProgress(IProgress<AsyncProgressEntry> baseProgress, double from, double range, string forceMessage = "",
                    bool ignoreIndeterminate = true) {
                _baseProgress = baseProgress;
                _from = from;
                _range = range;
                _forceMessage = forceMessage;
                _ignoreIndeterminate = ignoreIndeterminate;
            }

            public void Report(AsyncProgressEntry value) {
                if (_ignoreIndeterminate && value.IsIndeterminate) return;
                _baseProgress?.Report(new AsyncProgressEntry(
                        _forceMessage == "" ? value.Message : string.Format(_forceMessage, value.Message.ToSentenceMember()), _from + value.Progress * _range));
            }
        }

        [NotNull]
        public static IProgress<AsyncProgressEntry> Subrange([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, double from, double range,
                bool ignoreIndeterminate = true) {
            return new SubrangeProgress(baseProgress, from, range, ignoreIndeterminate: ignoreIndeterminate);
        }

        [NotNull]
        public static IProgress<AsyncProgressEntry> StringConvert([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, Func<string, string> convertFunc) {
            return new Progress<AsyncProgressEntry>(v => baseProgress?.Report(convertFunc(v.Message), v.Progress));
        }

        [NotNull]
        public static Tuple<IProgress<AsyncProgressEntry>, IProgress<AsyncProgressEntry>> Split([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress,
                double splitPos, bool ignoreIndeterminate = true) {
            return Tuple.Create<IProgress<AsyncProgressEntry>, IProgress<AsyncProgressEntry>>(
                    new SubrangeProgress(baseProgress, 0d, splitPos, ignoreIndeterminate: ignoreIndeterminate),
                    new SubrangeProgress(baseProgress, splitPos, 1d - splitPos, ignoreIndeterminate: ignoreIndeterminate));
        }

        [NotNull]
        public static IProgress<double> ToDoubleProgress([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, string message) {
            return new Progress<double>(v => baseProgress.Report(message, v));
        }

        [NotNull]
        public static IProgress<AsyncProgressEntry> Subrange([CanBeNull] this IProgress<AsyncProgressEntry> baseProgress, double from, double range,
                string forceMessage, bool ignoreIndeterminate = true) {
            return new SubrangeProgress(baseProgress, from, range, forceMessage, ignoreIndeterminate);
        }

        public static void Report([CanBeNull] this IProgress<AsyncProgressEntry> progress, string message, double? value) {
            progress?.Report(new AsyncProgressEntry(message, value));
        }

        public static void Report([CanBeNull] this IProgress<AsyncProgressEntry> progress, string message, int i, int total) {
            progress?.Report(new AsyncProgressEntry(message, i, total));
        }

        /*public static void Report([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, double? value) {
            progress?.Report(new Tuple<string, double?>(message, value));
        }

        public static void Report([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, int i, int total) {
            progress?.Report(new Tuple<string, double?>(message, (double)i / total));
        }*/

        private class SubrangeTupleProgress : IProgress<Tuple<string, double?>> {
            private readonly IProgress<Tuple<string, double?>> _baseProgress;
            private readonly double _from;
            private readonly double _range;
            private readonly string _forceMessage;
            private readonly bool _ignoreIndeterminate;

            public SubrangeTupleProgress(IProgress<Tuple<string, double?>> baseProgress, double from, double range, string forceMessage = "",
                    bool ignoreIndeterminate = true) {
                _baseProgress = baseProgress;
                _from = from;
                _range = range;
                _forceMessage = forceMessage;
                _ignoreIndeterminate = ignoreIndeterminate;
            }

            public void Report(Tuple<string, double?> value) {
                if (_ignoreIndeterminate && value.IsIndeterminate()) return;
                _baseProgress?.Report(new Tuple<string, double?>(
                        _forceMessage == "" ? value.Item1 : string.Format(_forceMessage, value.Item1.ToSentenceMember()), _from + value.Item2 * _range));
            }
        }

        public static bool IsIndeterminate([CanBeNull] this Tuple<string, double?> baseProgress) {
            return baseProgress?.Item1 == "" && Equals(baseProgress.Item2, 0d);
        }

        [NotNull]
        public static IProgress<Tuple<string, double?>> SubrangeTuple([CanBeNull] this IProgress<Tuple<string, double?>> baseProgress, double from, double range,
                bool ignoreIndeterminate = true) {
            return new SubrangeTupleProgress(baseProgress, from, range, ignoreIndeterminate: ignoreIndeterminate);
        }

        [NotNull]
        public static IProgress<Tuple<string, double?>> SubrangeTuple([CanBeNull] this IProgress<Tuple<string, double?>> baseProgress, double from, double range,
                string forceMessage, bool ignoreIndeterminate = true) {
            return new SubrangeTupleProgress(baseProgress, from, range, forceMessage, ignoreIndeterminate);
        }

        public static void ReportTuple([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, double? value) {
            progress?.Report(new Tuple<string, double?>(message, value));
        }

        public static void ReportTuple([CanBeNull] this IProgress<Tuple<string, double?>> progress, string message, int i, int total) {
            const double x = 0.000001;
            progress?.Report(new Tuple<string, double?>(message, (double)i / total * (1d - 2d * x) + x));
        }
    }
}