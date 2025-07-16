using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Un4seen.Bass;
using WebRadio.ViewModels;

namespace WebRadio.Utils
{
    static internal class StreamHelper
    {
        public static IStream? CreateStream(string url, Options options, ILogger logger)
        {
            logger.LogDebug("Opening {Url}", url);

            var stream = Stream.Create(url, BASSFlag.BASS_STREAM_STATUS, (IntPtr buffer, int length, IntPtr user) =>
            {
                if (buffer != IntPtr.Zero && length == 0 && options.ShowDownloadInfo)
                {
                    var txt = Marshal.PtrToStringAnsi(buffer);

                    logger.LogDebug("Download info: {Info}", txt);
                }
            });

            if (stream == null)
            {
                logger.LogError("Could not create stream from {Url}", url);
                return null;
            }

            var channelInfo = stream.GetInfo();

            logger.LogDebug("Channel info: {ChannelInfo}", channelInfo);

            if (channelInfo.ctype == BASSChannelType.BASS_CTYPE_STREAM_MF)
            {
                var wftext = stream.GetTagsWAVEFORMAT();

                if (wftext != null)
                {
                    logger.LogDebug("Sample rate: {SampleRate}kbps", wftext.waveformatex.nAvgBytesPerSec * 8 / 1000);
                }
            }

            if (options.ShowICYTags)
            {
                var icy = stream.GetTagsICY() ?? stream.GetTagsHTTP() ?? [];

                foreach (var tag in icy)
                {
                    if (tag.StartsWith("icy-metaint:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        stream.HasMetadata = true;
                    }

                    logger.LogDebug("ICY tag: {Tag}", tag);
                }
            }

            return stream;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes >= 0x40000000)
            {
                return ((double)(bytes >> 20) / 1024).ToString("0.00 GB", CultureInfo.InvariantCulture);
            }

            if (bytes >= 0x100000)
            {
                return ((double)(bytes >> 10) / 1024).ToString("0.00 MB", CultureInfo.InvariantCulture);
            }

            return ((double)bytes / 1024).ToString("0.00 KB", CultureInfo.InvariantCulture);
        }
    }
}
