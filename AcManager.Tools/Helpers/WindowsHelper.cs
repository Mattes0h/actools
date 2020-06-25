﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public static class WindowsHelper {
        public const string RestartArg = "--restart";

        [Localizable(false), ContractAnnotation("=> halt")]
        public static void RestartCurrentApplication() {
            try {
                ProcessExtension.Start(MainExecutingFile.Location,
                        Environment.GetCommandLineArgs().Skip(1).ApartFrom(RestartArg)
                                   .Where(x => !x.StartsWith("acmanager:", StringComparison.OrdinalIgnoreCase)).Prepend(RestartArg));

                var app = Application.Current;
                if (app == null) {
                    Environment.Exit(0);
                } else {
                    app.Shutdown();
                }
            } catch (Exception e) {
                Logging.Warning(e);
            }
        }

        [Localizable(false)]
        public static void ViewDirectory([NotNull] string directory) {
            ProcessExtension.Start(@"explorer", new [] { FileUtils.NormalizePath(directory) });
        }

        [Localizable(false)]
        public static void ViewFile([NotNull] string filename) {
            Process.Start(new ProcessStartInfo {
                FileName = "explorer",
                WorkingDirectory = Path.GetDirectoryName(filename) ?? "",
                Arguments = "/select,\"" + Path.GetFileName(filename) + "\""
            });
        }

        [Localizable(false)]
        public static void ViewInBrowser([CanBeNull] string url) {
            if (string.IsNullOrWhiteSpace(url)) return;

            url = url.IsWebUrl() ? url : $"http://{url}";
            try {
                Process.Start(url);
            } catch (Exception) {
                NonfatalError.Notify("Can’t open link", $"App tried to open: “{url}”");
            }
        }

        [Localizable(false)]
        public static void ViewInBrowser([CanBeNull] Uri url) {
            ViewInBrowser(url?.AbsoluteUri);
        }

        [Localizable(false)]
        public static void OpenFile([NotNull] string filename) {
            Process.Start(filename);
        }

        [NotNull]
        public static IEnumerable<string> GetInputFiles(this DragEventArgs e) {
            return e.Data.GetInputFiles();
        }

        [NotNull]
        public static IEnumerable<string> GetInputFiles(this IDataObject data) {
            return data.GetData(DataFormats.FileDrop) as string[] ??
                    (data.GetData(DataFormats.UnicodeText) as string)?.Split('\n')
                                                                        .Select(x => x.Trim())
                                                                        .Select(x => x.Length > 1 && x.StartsWith(@"""") && x.EndsWith(@"""")
                                                                                ? x.Substring(1, x.Length - 2) : x) ?? new string[0];
        }
    }
}
