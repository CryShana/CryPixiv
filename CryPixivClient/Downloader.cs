using CryPixivClient.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CryPixivClient
{
    public class Downloader
    {
        public long TotalImagesCount { get; private set; }
        public long DownloadedImagesCount { get; private set; }
        public double Percentage => (TotalImagesCount == 0) ? 0.0 : (DownloadedImagesCount / (double)TotalImagesCount) * 100.0;

        /// <summary>
        /// Occurs when all downloads are finished
        /// </summary>
        public event EventHandler Finished;
        /// <summary>
        /// Occurs when one page gets downloaded and written to a file. Tuple contains Work Id and Page Number.
        /// </summary>
        public event EventHandler<Tuple<long, int>> DownloadFinished;
        /// <summary>
        /// Occurs when downloaded encounters an error while downloading a page. Tuple contains Work Id, Page Number and Exception message.
        /// </summary>
        public event EventHandler<Tuple<long, int, string>> ErrorEncountered;

        Queue<PixivWork> toDownload;
        public string Destination { get; }

        bool IsStopped = false;
        public bool IsStarted { get; private set; }
        public bool IsRunning { get; private set; }

        IProgress<DownloaderProgress> DownloadProgress;

        /// <summary>
        /// Use this to download images
        /// </summary>
        /// <param name="toDownload">Pixiv works to be downloaded</param>
        /// <param name="destinationFolder">Destination folder</param>
        /// <param name="downloadProgress">For the purposes of tracking progress</param>
        public Downloader(List<PixivWork> toDownload, string destinationFolder, IProgress<DownloaderProgress> downloadProgress = null)
        {
            this.toDownload = new Queue<PixivWork>(toDownload);
            this.Destination = destinationFolder;
            this.DownloadProgress = downloadProgress;
            
            foreach(var post in toDownload) TotalImagesCount += post.PageCount ?? 1;
        }
        
        public void Start()
        {
            if (IsStarted)
            {
                IsStarted = false;
                return;
            }

            IsStarted = true;
            if (IsRunning == false)
            {
                Task.Run(() => StartDownloading());
                IsRunning = true;
            }
        }

        public void Pause()
        {
            IsStarted = false;
        }

        public void Stop()
        {
            IsStarted = false;
            IsStopped = true;
        }

        async Task StartDownloading()
        {
            while (toDownload.Count > 0)
            {
                if (IsStarted == false || IsStopped)
                {
                    if (IsStopped) break;
                    await Task.Delay(300);
                    continue;
                }

                // get next work
                var work = toDownload.Dequeue();

                // go through each page
                for(int i = 0; i < work.PageCount; i++)
                {
                    try
                    {
                        if (IsStarted == false || IsStopped)
                        {
                            while (true)
                            {
                                if (IsStarted) break;
                                await Task.Delay(300);
                                continue;
                            }

                            if (IsStopped) break;
                        }

                        var uri = work.GetImageUri(work.ImageUrls.Large, i);

                        var extension = uri.Substring(uri.LastIndexOf('.') + 1).ToLower();
                        if (extension != "png" && extension != "gif") extension = "jpg";

                        var filename = GetValidFilename(work.Id.Value.ToString(), i, extension);
                        var fullpath = Path.Combine(Destination, filename);
                        DownloadProgress?.Report(new DownloaderProgress(work, i, fullpath, 0.0, Percentage));

                        byte[] buffer = null;
                        using (var client = new WebClient())
                        {
                            client.Headers.Add("Referer", "http://spapi.pixiv.net/");
                            client.Headers.Add("User-Agent", "PixivIOSApp/5.8.0");
                            client.UseDefaultCredentials = true;

                            client.DownloadProgressChanged += (a, b) =>
                            {
                                DownloadProgress?.Report(new DownloaderProgress(work, i, fullpath, b.ProgressPercentage, Percentage));
                            };

                            buffer = await client.DownloadDataTaskAsync(uri);
                        }

                        File.WriteAllBytes(fullpath, buffer);
                        DownloadedImagesCount++;
                        DownloadProgress?.Report(new DownloaderProgress(work, i, fullpath, 100.0, Percentage));
                        DownloadFinished?.Invoke(this, new Tuple<long, int>(work.Id.Value, i));
                    }
                    catch(Exception ex)
                    {
                        DownloadedImagesCount++;
                        ErrorEncountered?.Invoke(this, new Tuple<long, int, string>(work.Id.Value, i, ex.Message));
                    }
                }
            }

            IsRunning = false;
            Finished?.Invoke(this, EventArgs.Empty);
        }

        string GetValidFilename(string id, int page, string extension)
        {
            string filename = $"{id}_p{page}.{extension}";
            string fullpath = Path.Combine(Destination, filename);

            long num = 0;
            while (File.Exists(fullpath))
            {
                filename = $"{id}_p{page}_{num}.{extension}";
                fullpath = Path.Combine(Destination, filename);
                num++;

                if (num >= 100000) throw new InvalidOperationException();
            }

            return filename;
        }

        public class DownloaderProgress
        {
            public PixivWork AssociatedWork { get; set; }
            public int PageNumber { get; set; }
            public string FileName { get; set; }
            public double Progress { get; set; }
            public double TotalProgress { get; set; }

            public DownloaderProgress(PixivWork work, int pageNumber, string filename, 
                double progress, double totalProgress)
            {
                this.AssociatedWork = work;
                this.PageNumber = pageNumber;
                this.FileName = filename;
                this.Progress = progress;
                this.TotalProgress = totalProgress;
            }
        }
    }
}
