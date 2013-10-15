﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace ImageDownloader
{
    class Program
    {
        static List<char> alph = new List<char>();
       
        static char[] startUrl = "aaaaa".ToCharArray();
        
 
        static void Main(string[] args)
        {
            for (char c = 'a'; c <= 'z'; ++c)
            {
                alph.Add(c);
            }
            alph.AddRange(Enumerable.Range(1, 9).Select(x => x.ToString()[0]).ToArray());

            HtmlReaderManager hrm = new HtmlReaderManager();
            LimitedConcurrencyLevelTaskScheduler lcts =
               new LimitedConcurrencyLevelTaskScheduler(200);
            TaskFactory factory = new TaskFactory(lcts);
            var tasks = new List<Task>();

            int counter = 1;


            foreach (string url in GetNextUrl())
            {

                
                Options opt = new Options();
                opt.Url = url;
                opt.counter = counter;

                tasks.Add(factory.StartNew((opts) =>
                                               {
                                                   bool isDownloaded = false;
                                                   int tryCount = 0;
                                                   do
                                                   {
                                                       Options options = (Options) opts;
                                                       string targetUrl = "http://prntscr.com/1" + options.Url;
                                                       
                                                       try
                                                       {
                                                           hrm.Get(targetUrl);
                                                           string html = hrm.Html;
                                                           HtmlAgilityPack.HtmlDocument doc = new HtmlDocument();
                                                           doc.LoadHtml(html);
                                                           string imgUrl = doc.DocumentNode.SelectSingleNode("//meta[@name='twitter:image:src']").Attributes["content"].Value;
                                                           if (!string.IsNullOrEmpty(imgUrl))
                                                           {
                                                               ImageDownloader.DownloadRemoteImageFile(imgUrl, "images/" + Guid.NewGuid() + ".jpeg");
                                                               Console.WriteLine(options.counter + ". Скачиваем " + imgUrl);
                                                               
                                                           }
                                                           else
                                                               Console.WriteLine(options.counter + ". " + imgUrl + " пуст");
                                                           break;
                                                       }
                                                       catch (Exception ex)
                                                       {
                                                           tryCount++;
                                                           if (ex.ToString().StartsWith("System.Net.WebException: The remote server returned an error: (503) Server Unavailable.") && tryCount<20)
                                                           {
                                                               Random s = new Random();
                                                               int randomVal = s.Next(1000, 10000);
                                                               Thread.Sleep(randomVal);
                                                               Console.WriteLine(options.counter + ". ждём " + targetUrl +" ("+tryCount+" раз)");
                                                           }
                                                           else
                                                           {
                                                               Console.WriteLine(options.counter + ". ошибка " + targetUrl);
                                                               isDownloaded = true;
                                                           }
                                                           
                                                       }
                                                   } while (!isDownloaded);
                                               }, opt, TaskCreationOptions.LongRunning));

                if (counter%200 == 0)
                    Task.WaitAll(tasks.ToArray());
                tasks.Clear();
                counter++;
            }

            Console.ReadLine();

        }

        

        private static IEnumerable GetNextUrl(int checkCharIndex=0)
        {
                foreach (char c in alph)
                {
                    startUrl[checkCharIndex] = c;
                    
                    if((startUrl.Length-1)!=checkCharIndex)
                    {
                        foreach (var innC in GetNextUrl(checkCharIndex + 1))
                        {
                            yield return innC;
                        }
                    }
                    else
                    {
                        yield return new string(startUrl);
                    }
                }
        }
    }

    public struct Options
        {
            public string Url;
            public int counter;
        }
    public class ImageDownloader
    {
        public static void DownloadRemoteImageFile(string uri, string fileName)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            // Check that the remote file was found. The ContentType
            // check is performed since a request for a non-existent
            // image file might be redirected to a 404-page, which would
            // yield the StatusCode "OK", even though the image was not
            // found.
            if ((response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Moved ||
                response.StatusCode == HttpStatusCode.Redirect) &&
                response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {

                // if the remote file was found, download oit
                using (Stream inputStream = response.GetResponseStream())
                using (Stream outputStream = File.OpenWrite(fileName))
                {
                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    do
                    {
                        bytesRead = inputStream.Read(buffer, 0, buffer.Length);
                        outputStream.Write(buffer, 0, bytesRead);
                    } while (bytesRead != 0);
                }
            }
        }
    }
    /// <summary> 
    /// Provides a task scheduler that ensures a maximum concurrency level while 
    /// running on top of the ThreadPool. 
    /// http://msdn.microsoft.com/en-us/library/ee789351.aspx
    /// </summary> 
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        /// <summary>Whether the current thread is processing work items.</summary>
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;
        /// <summary>The list of tasks to be executed.</summary> 
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks) 
        /// <summary>The maximum concurrency level allowed by this scheduler.</summary> 
        private readonly int _maxDegreeOfParallelism;
        /// <summary>Whether the scheduler is currently processing work items.</summary> 
        private int _delegatesQueuedOrRunning = 0; // protected by lock(_tasks) 

        /// <summary> 
        /// Initializes an instance of the LimitedConcurrencyLevelTaskScheduler class with the 
        /// specified degree of parallelism. 
        /// </summary> 
        /// <param name="maxDegreeOfParallelism">The maximum degree of parallelism provided by this scheduler.</param>
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>Queues a task to the scheduler.</summary> 
        /// <param name="task">The task to be queued.</param>
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        /// <summary> 
        /// Informs the ThreadPool that there's work to be executed for this scheduler. 
        /// </summary> 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items. 
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue. 
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed, 
                            // note that we're done processing, and get out. 
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue 
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread 
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        /// <summary>Attempts to execute the specified task on the current thread.</summary> 
        /// <param name="task">The task to be executed.</param>
        /// <param name="taskWasPreviouslyQueued"></param>
        /// <returns>Whether the task could be executed on the current thread.</returns> 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining 
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue 
            if (taskWasPreviouslyQueued) TryDequeue(task);

            // Try to run the task. 
            return base.TryExecuteTask(task);
        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary> 
        /// <param name="task">The task to be removed.</param>
        /// <returns>Whether the task could be found and removed.</returns> 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        /// <summary>Gets the maximum concurrency level supported by this scheduler.</summary> 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        /// <summary>Gets an enumerable of the tasks currently scheduled on this scheduler.</summary> 
        /// <returns>An enumerable of the tasks currently scheduled.</returns> 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks.ToArray();
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }
}