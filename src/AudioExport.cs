using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace VideoToMp3
{
    internal sealed class ExportProgress
    {
        public readonly int Percent;
        public readonly string Message;
        public ExportProgress(int percent, string message) { Percent = percent; Message = message; }
    }

    internal static class AudioExport
    {
        const int Rate = 48000;
        const int BytesPerFrame = 4; // 16-bit stereo PCM
        const string Mp3 = "-map 0:a:0 -c:a libmp3lame -b:a 192k -write_xing 1 ";

        // All quoted arguments are absolute file paths. Windows file names cannot contain quotes.
        static string Quote(string path) { return "\"" + path + "\""; }
        static string Seconds(long frames)
        {
            return ((decimal)frames / Rate).ToString("0.000000000", CultureInfo.InvariantCulture);
        }

        sealed class Ffmpeg : IDisposable
        {
            readonly Process process = new Process();
            readonly StringBuilder errors = new StringBuilder();
            readonly CancellationToken token;
            readonly CancellationTokenRegistration registration;
            readonly string operation;
            public Stream Input { get { return process.StandardInput.BaseStream; } }
            public Stream Output { get { return process.StandardOutput.BaseStream; } }

            public Ffmpeg(string args, string operation, CancellationToken token, bool input = false, bool output = false,
                          string workingDirectory = null, Action<TimeSpan> onPosition = null)
            {
                this.token = token;
                this.operation = operation;
                token.ThrowIfCancellationRequested();
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                process.StartInfo = new ProcessStartInfo(File.Exists(local) ? local : "ffmpeg.exe",
                    "-hide_banner -nostdin -loglevel error -xerror " + args)
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardInput = input, RedirectStandardOutput = output,
                    StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = workingDirectory ?? ""
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    if (onPosition != null && e.Data.StartsWith("out_time_us=", StringComparison.Ordinal))
                    {
                        long us;
                        if (long.TryParse(e.Data.Substring(12), out us) && us >= 0)
                            onPosition(TimeSpan.FromTicks(us * 10));
                        return;
                    }
                    if (onPosition != null && System.Text.RegularExpressions.Regex.IsMatch(e.Data, @"^\w+=")) return;
                    lock (errors)
                    {
                        errors.AppendLine(e.Data);
                        if (errors.Length > 3000) errors.Remove(0, errors.Length - 3000);
                    }
                };
                try { process.Start(); }
                catch (System.ComponentModel.Win32Exception e)
                {
                    process.Dispose();
                    throw new IOException("Не удалось запустить FFmpeg. Установите FFmpeg или поместите его рядом с программой.", e);
                }
                registration = token.Register(Stop);
                process.BeginErrorReadLine();
            }
            void Stop()
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            public void Finish()
            {
                process.WaitForExit();
                token.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                    throw new IOException(operation + Environment.NewLine + errors.ToString().Trim());
            }
            public void Dispose()
            {
                registration.Dispose();
                Stop();
                process.WaitForExit();
                process.Dispose();
            }
        }

        public static string Run(string[] videos, string destination, int partMinutes, bool saveMp3,
                                 string speechLanguage, CancellationToken token, Action<ExportProgress> progress)
        {
            if (videos.Length == 0 || partMinutes < 0) throw new ArgumentException("Проверьте список файлов и длительность.");
            if (!saveMp3 && speechLanguage == null) throw new ArgumentException("Выберите формат результата.");
            if (!saveMp3 && partMinutes != 0) throw new ArgumentException("Разбиение доступно при сохранении MP3.");
            if (speechLanguage != null && speechLanguage != "ru" && speechLanguage != "auto" && speechLanguage != "en")
                throw new ArgumentException("Неизвестный язык речи.");
            Directory.CreateDirectory(destination);
            string stage = Path.Combine(destination, ".mp3-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string master = Path.Combine(stage, "master.mp3");
            string lossless = Path.Combine(stage, "joined.flac");
            string parts = Path.Combine(stage, "parts");
            string transcript = speechLanguage == null ? null : Path.Combine(stage, "transcript.txt");
            try
            {
                bool split = partMinutes > 0;
                bool join = saveMp3 || videos.Length > 1;
                string targets = saveMp3 ? Mp3 + Quote(master) : "";
                if (split || !saveMp3) targets += " -map 0:a:0 -c:a flac -compression_level 1 " + Quote(lossless);
                long bytes = join ? Join(videos, targets, token, progress) : 0;
                if (split) SaveParts(lossless, parts, bytes / BytesPerFrame, partMinutes, token, progress);
                if (transcript != null)
                {
                    string source = !join ? videos[0] : (split || !saveMp3 ? lossless : master);
                    Transcribe(source, transcript, speechLanguage, token, progress);
                }
                token.ThrowIfCancellationRequested();
                string stem = videos.Length == 1 ? Path.GetFileNameWithoutExtension(videos[0]) : "Общее аудио";
                return Publish(stem, destination, saveMp3 ? master : null, split ? parts : null, transcript);
            }
            catch (Exception) { token.ThrowIfCancellationRequested(); throw; }
            finally { Directory.Delete(stage, true); }
        }

        static long Join(string[] videos, string targets, CancellationToken token, Action<ExportProgress> progress)
        {
            long bytes = 0;
            using (var encoder = new Ffmpeg("-f s16le -ar 48000 -ac 2 -i pipe:0 " + targets,
                "Ошибка записи объединённого аудио.", token, input: true))
            {
                byte[] buffer = new byte[131072];
                for (int i = 0; i < videos.Length; i++)
                {
                    string name = Path.GetFileName(videos[i]);
                    string step = String.Format("Извлечение {0} из {1}  ·  {2}", i + 1, videos.Length, name);
                    progress(new ExportProgress(-1, step));
                    var update = Stopwatch.StartNew();
                    using (var decoder = new Ffmpeg("-i " + Quote(videos[i]) +
                        " -map 0:a:0 -vn -sn -dn -ac 2 -ar 48000 -f s16le pipe:1",
                        "Не удалось извлечь звук из файла «" + name + "». Проверьте его аудиодорожку.", token, output: true))
                    {
                        long before = bytes;
                        int count;
                        while ((count = decoder.Output.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            try { encoder.Input.Write(buffer, 0, count); }
                            catch (IOException) { encoder.Finish(); throw; }
                            bytes += count;
                            if (update.ElapsedMilliseconds >= 650)
                            {
                                TimeSpan duration = TimeSpan.FromSeconds(bytes / BytesPerFrame / Rate);
                                progress(new ExportProgress(-1, duration.ToString("g") + "  ·  " + step));
                                update.Restart();
                            }
                        }
                        decoder.Finish();
                        if (bytes == before) throw new IOException("Пустая аудиодорожка в файле «" + name + "».");
                    }
                }
                encoder.Input.Close();
                encoder.Finish();
            }

            return bytes;
        }

        static void SaveParts(string lossless, string parts, long frames, int partMinutes,
                              CancellationToken token, Action<ExportProgress> progress)
        {
            // Reserve two MP3 frames for encoder delay and padding in the displayed duration.
            long limit = partMinutes * 60L * Rate - 2 * 1152;
            long total = (frames + limit - 1) / limit;
            Directory.CreateDirectory(parts);
            for (long index = 0; index < total; index++)
            {
                long start = index * limit;
                long length = Math.Min(limit, frames - start);
                string number = (index + 1).ToString("D" + Math.Max(2, total.ToString().Length));
                progress(new ExportProgress(70 + (int)(index * 29 / total),
                    String.Format("Сохранение части {0} из {1}", index + 1, total)));
                string path = Path.Combine(parts, "Часть " + number + ".mp3");
                // Seek in the lossless master so that cuts also work across video boundaries.
                using (var part = new Ffmpeg("-ss " + Seconds(start) + " -i " + Quote(lossless) +
                    " -af atrim=end_sample=" + length.ToString(CultureInfo.InvariantCulture) + " " + Mp3 + Quote(path),
                    "Не удалось сохранить часть " + number + ".", token)) part.Finish();
            }
        }

        static void Transcribe(string source, string transcript, string language, CancellationToken token,
                               Action<ExportProgress> progress)
        {
            string models = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            const string model = "ggml-small-q5_1.bin";
            const string vad = "ggml-silero-v6.2.0.bin";
            if (!File.Exists(Path.Combine(models, model)) || !File.Exists(Path.Combine(models, vad)))
                throw new FileNotFoundException("Для распознавания рядом с программой нужна папка models с моделями Whisper и Silero.");
            progress(new ExportProgress(-1, "Распознавание речи на этом компьютере…"));
            string filter = "aresample=16000,asetpts=N/SR/TB,whisper=model=" + model + ":vad_model=" + vad +
                ":vad_min_speech_duration=7:vad_min_silence_duration=1:language=" + language + ":queue=30:destination=-:format=srt";
            string args = "-stats_period 2 -progress pipe:2 -filter_threads " + Math.Min(6, Environment.ProcessorCount) +
                " -i " + Quote(source) + " -map 0:a:0 -vn -af " + filter + " -f null NUL";
            using (var speech = new Ffmpeg(args, "Не удалось распознать речь. Проверьте аудиодорожку, модели и поддержку фильтра whisper в FFmpeg.",
                token, output: true, workingDirectory: models, onPosition: t =>
                    progress(new ExportProgress(-1, "Распознавание речи  ·  " + Clock(t)))))
            using (var reader = new StreamReader(speech.Output, Encoding.UTF8))
            using (var writer = new StreamWriter(transcript, false, new UTF8Encoding(true)))
            {
                bool hasSpeech = WriteTranscript(reader, writer);
                speech.Finish();
                if (!hasSpeech) writer.WriteLine("Речь не распознана.");
            }
        }

        static string Clock(TimeSpan time)
        {
            return string.Format("{0:00}:{1:00}:{2:00}", (long)time.TotalHours, time.Minutes, time.Seconds);
        }

        // FFmpeg sends numbered SRT cues with UTF-8 text to stdout.
        internal static bool WriteTranscript(TextReader reader, TextWriter writer)
        {
            string line;
            bool hasSpeech = false;
            while ((line = reader.ReadLine()) != null)
            {
                int end = line.IndexOf(',');
                if (end <= 0 || !line.Contains(" --> ")) continue;
                string stamp = line.Substring(0, end);
                var text = new StringBuilder();
                while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
                {
                    if (text.Length > 0) text.Append(' ');
                    text.Append(line.Trim());
                }
                if (text.Length == 0) continue;
                writer.WriteLine("[{0}] {1}", stamp, text);
                hasSpeech = true;
            }
            return hasSpeech;
        }

        static bool Exists(string path) { return File.Exists(path) || Directory.Exists(path); }

        static string Publish(string stem, string destination, string master, string parts, string transcript)
        {
            for (int n = 1; ; n++)
            {
                string name = n == 1 ? stem : stem + " (" + n + ")";
                string path = Path.Combine(destination, name + ".mp3");
                string folder = Path.Combine(destination, name + " — части");
                string txt = Path.Combine(destination, name + ".txt");
                if ((master != null && Exists(path)) || (transcript != null && Exists(txt)) || (parts != null && Exists(folder))) continue;
                bool movedMp3 = false, movedTxt = false;
                try
                {
                    if (master != null) { File.Move(master, path); movedMp3 = true; }
                    if (transcript != null) { File.Move(transcript, txt); movedTxt = true; }
                    if (parts != null) Directory.Move(parts, folder);
                    return master == null ? txt : path;
                }
                catch (IOException)
                {
                    if (movedTxt) File.Move(txt, transcript);
                    if (movedMp3) File.Move(path, master);
                    if ((master != null && Exists(path)) || (transcript != null && Exists(txt)) || (parts != null && Exists(folder))) continue;
                    throw;
                }
                catch
                {
                    if (movedTxt) File.Move(txt, transcript);
                    if (movedMp3) File.Move(path, master);
                    throw;
                }
            }
        }
    }
}
