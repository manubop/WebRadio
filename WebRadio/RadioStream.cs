using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Tags;
using WebRadio.DataModel;

namespace WebRadio
{
    internal interface IRadioStream : IDisposable
    {
        bool HasMetadata { get; set; }
        int Handle { get; }
        int SetSyncProc(BASSSync type, Action<int, int, int, IntPtr> action);
        BASS_CHANNELINFO GetInfo();
        WAVEFORMATEXT GetTagsWAVEFORMAT();
        string[] GetTagsICY();
        string[] GetTagsHTTP();
        bool GetTagInfo(TAG_INFO tagInfo);
        int GetLevel();
        bool GetAttribute(BASSAttribute attribute, ref float val);
        bool SetAttribute(BASSAttribute attribute, float val);
        long GetDownloadFilePosition();
        BASSActive IsActive();
        bool Start();
        bool Stop();
        bool Play(bool restart);
        bool Pause();
        int SetupTagDisplay(string url, Action<SongInfo> action);
    }

    internal sealed class RadioStream(int stream, DOWNLOADPROC downloadProc, params SYNCPROC[] syncProcs) : IRadioStream
    {
        private readonly GCHandle _downloadProcHandle = GCHandle.Alloc(downloadProc);
        private readonly ICollection<GCHandle> _syncProcHandles = [.. syncProcs.Select(syncProc => GCHandle.Alloc(syncProc))];

        public bool HasMetadata { get; set; }

        public int Handle => stream;

        public static async Task<IRadioStream?> Create(string url, BASSFlag flags, Action<IntPtr, int, IntPtr> downloadAction)
        {
            return await Task.Run(() =>
            {
                var downloadProc = new DOWNLOADPROC(downloadAction);
                var stream = Bass.BASS_StreamCreateURL(url, 0, flags, downloadProc, IntPtr.Zero);

                if (stream == 0)
                {
                    return null;
                }

                return new RadioStream(stream, downloadProc);
            });
        }

        public int SetSyncProc(BASSSync type, Action<int, int, int, IntPtr> action)
        {
            var syncProc = new SYNCPROC(action);

            _syncProcHandles.Add(GCHandle.Alloc(syncProc));

            return Bass.BASS_ChannelSetSync(stream, type, 0, syncProc, IntPtr.Zero);
        }

        public BASS_CHANNELINFO GetInfo() => Bass.BASS_ChannelGetInfo(stream);

        public WAVEFORMATEXT GetTagsWAVEFORMAT() => Bass.BASS_ChannelGetTagsWAVEFORMAT(stream);

        public string[] GetTagsICY() => Bass.BASS_ChannelGetTagsICY(stream);

        public string[] GetTagsHTTP() => Bass.BASS_ChannelGetTagsHTTP(stream);

        public bool GetTagInfo(TAG_INFO tagInfo) => BassTags.BASS_TAG_GetFromURL(stream, tagInfo);

        public int GetLevel() => Bass.BASS_ChannelGetLevel(stream);

        public bool GetAttribute(BASSAttribute attribute, ref float val) => Bass.BASS_ChannelGetAttribute(stream, attribute, ref val);

        public bool SetAttribute(BASSAttribute attribute, float val) => Bass.BASS_ChannelSetAttribute(stream, attribute, val);
        public long GetDownloadFilePosition() => Bass.BASS_StreamGetFilePosition(stream, BASSStreamFilePosition.BASS_FILEPOS_DOWNLOAD);


        public BASSActive IsActive() => Bass.BASS_ChannelIsActive(stream);

        public bool Start() => Bass.BASS_ChannelStart(stream);

        public bool Stop() => Bass.BASS_ChannelStop(stream);

        public bool Play(bool restart) => Bass.BASS_ChannelPlay(stream, restart);

        public bool Pause() => Bass.BASS_ChannelPause(stream);

        public int SetupTagDisplay(string url, Action<SongInfo> action)
        {
            var tagInfo = new TAG_INFO(url);

            if (GetTagInfo(tagInfo))
            {
                action(new SongInfo(tagInfo.artist, tagInfo.title));
            }

            return SetSyncProc(BASSSync.BASS_SYNC_META, (handle, channel, data, user) =>
            {
                var tags = Bass.BASS_ChannelGetTags(channel, BASSTag.BASS_TAG_META);

                if (!tagInfo.UpdateFromMETA(tags, true, true))
                {
                    return;
                }

                action(new SongInfo(tagInfo.artist, tagInfo.title));
            });
        }

        public void Dispose()
        {
            Bass.BASS_StreamFree(stream);

            _downloadProcHandle.Free();

            foreach (var handle in _syncProcHandles)
            {
                handle.Free();
            }
        }
    }
}
