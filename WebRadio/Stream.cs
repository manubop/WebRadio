using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Un4seen.Bass;
using Un4seen.Bass.AddOn.Tags;

namespace WebRadio
{
    internal interface IStream : IDisposable
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
        long GetFilePosition(BASSStreamFilePosition pos);
        BASSActive IsActive();
        bool Start();
        bool Stop();
        bool Play(bool restart);
        bool Pause();
        void SetupTagDisplay(string url, Action<TAG_INFO> action);
    }

    internal sealed class Stream(int stream, DOWNLOADPROC downloadProc, params SYNCPROC[] syncProcs) : IStream
    {
        private readonly GCHandle _downloadProcHandle = GCHandle.Alloc(downloadProc);
        private readonly List<GCHandle> _syncProcHandles = syncProcs.Select(syncProc => GCHandle.Alloc(syncProc)).ToList();

        public bool HasMetadata { get; set; }

        public int Handle => stream;

        public static IStream? Create(string url, BASSFlag flags, Action<IntPtr, int, IntPtr> downloadAction)
        {
            var downloadProc = new DOWNLOADPROC(downloadAction);
            var stream = Bass.BASS_StreamCreateURL(url, 0, flags, downloadProc, IntPtr.Zero);

            if (stream == 0)
            {
                return null;
            }

            return new Stream(stream, downloadProc);
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

        public long GetFilePosition(BASSStreamFilePosition pos) => Bass.BASS_StreamGetFilePosition(stream, pos);

        public BASSActive IsActive() => Bass.BASS_ChannelIsActive(stream);

        public bool Start() => Bass.BASS_ChannelStart(stream);

        public bool Stop() => Bass.BASS_ChannelStop(stream);

        public bool Play(bool restart) => Bass.BASS_ChannelPlay(stream, restart);

        public bool Pause() => Bass.BASS_ChannelPause(stream);

        public void SetupTagDisplay(string url, Action<TAG_INFO> action)
        {
            var tagInfo = new TAG_INFO(url);

            if (GetTagInfo(tagInfo))
            {
                action(tagInfo);
            }

            SetSyncProc(BASSSync.BASS_SYNC_META, (int handle, int channel, int data, IntPtr user) =>
            {
                var tags = Bass.BASS_ChannelGetTags(channel, BASSTag.BASS_TAG_META);

                if (tagInfo.UpdateFromMETA(tags, true, true))
                {
                    action(tagInfo);
                }
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