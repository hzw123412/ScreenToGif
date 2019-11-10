﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ScreenToGif.Util
{
    internal class GifskiInterop
    {
        public Version Version { get; set; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GifskiSettings
        {
            public GifskiSettings(byte quality, bool looped, bool fast)
            {
                Width = 0;
                Height = 0;
                Quality = quality;
                Once = !looped;
                Fast = fast;
            }

            /// <summary>
            /// Resize to max this width if non-0.
            /// </summary>
            internal uint Width;

            /// <summary>
            /// Resize to max this height if width is non-0. Note that aspect ratio is not preserved.
            /// </summary>
            internal uint Height;

            /// <summary>
            /// 1-100. Recommended to set to 100.
            /// </summary>
            internal byte Quality;

            /// <summary>
            /// If true, looping is disabled.
            /// </summary>
            internal bool Once;

            /// <summary>
            /// Lower quality, but faster encode.
            /// </summary>
            internal bool Fast;
        }

        internal enum GifskiError
        {
            /// <summary>
            /// Alright.
            /// </summary>
            Ok = 0,

            /// <summary>
            /// One of input arguments was NULL.
            /// </summary>
            NullArgument,

            /// <summary>
            /// A one-time function was called twice, or functions were called in wrong order.
            /// </summary>
            InvalidState,

            /// <summary>
            /// Internal error related to palette quantization.
            /// </summary>
            QuantizationError,

            /// <summary>
            /// Internal error related to gif composing.
            /// </summary>
            GifError,

            /// <summary>
            /// Internal error related to multithreading.
            /// </summary>
            ThreadLost,

            /// <summary>
            /// I/O error: file or directory not found.
            /// </summary>
            NotFound,

            /// <summary>
            /// I/O error: permission denied.
            /// </summary>
            PermissionDenied,

            /// <summary>
            /// I/O error: File already exists.
            /// </summary>
            AlreadyExists,

            /// <summary>
            /// Misc I/O error.
            /// </summary>
            InvalidInput,

            /// <summary>
            /// Misc I/O error.
            /// </summary>
            TimedOut,

            /// <summary>
            /// Misc I/O error.
            /// </summary>
            WriteZero,

            /// <summary>
            /// Misc I/O error.
            /// </summary>
            Interrupted,

            /// <summary>
            /// Misc I/O error.
            /// </summary>
            UnexpectedEof,

            /// <summary>
            /// Should not happen, file a bug.
            /// </summary>
            OtherError
        }

        private delegate IntPtr NewDelegate(GifskiSettings settings);
        private delegate GifskiError AddPngFrameDelegate(IntPtr handle, uint index, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, ushort delay);
        private delegate GifskiError AddRgbaFrameDelegate(IntPtr handle, uint index, uint width, uint height, IntPtr pixels, int delay);

        private delegate GifskiError SetFileOutputDelegate(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        private delegate GifskiError FinishDelegate(IntPtr handle);

        private delegate GifskiError EndAddingFramesDelegate(IntPtr handle);
        private delegate GifskiError WriteDelegate(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);
        private delegate void DropDelegate(IntPtr handle);

        private readonly NewDelegate _new;
        private readonly AddPngFrameDelegate _addPngFrame;
        private readonly AddRgbaFrameDelegate _addRgbaFrame;

        private readonly SetFileOutputDelegate _setFileOutput;
        private readonly FinishDelegate _finish;

        private readonly EndAddingFramesDelegate _endAddingFrames;
        private readonly WriteDelegate _write;
        private readonly DropDelegate _drop;

        public GifskiInterop()
        {
            #region Get Gifski version

            var info = new FileInfo(UserSettings.All.GifskiLocation);
            info.Refresh();

            Version = info.Length == 502_208 ? new Version(0, 9, 3) : new Version(0, 0);

            #endregion

            #region Load functions

            _new = (NewDelegate)FunctionLoader.LoadFunction<NewDelegate>(UserSettings.All.GifskiLocation, "gifski_new");
            _addPngFrame = (AddPngFrameDelegate)FunctionLoader.LoadFunction<AddPngFrameDelegate>(UserSettings.All.GifskiLocation, "gifski_add_frame_png_file");
            _addRgbaFrame = (AddRgbaFrameDelegate)FunctionLoader.LoadFunction<AddRgbaFrameDelegate>(UserSettings.All.GifskiLocation, "gifski_add_frame_rgba");

            if (Version.Major == 0 && Version.Minor < 9)
            {
                //Older versions of the library.
                _endAddingFrames = (EndAddingFramesDelegate)FunctionLoader.LoadFunction<EndAddingFramesDelegate>(UserSettings.All.GifskiLocation, "gifski_end_adding_frames");
                _write = (WriteDelegate)FunctionLoader.LoadFunction<WriteDelegate>(UserSettings.All.GifskiLocation, "gifski_write");
                _drop = (DropDelegate)FunctionLoader.LoadFunction<DropDelegate>(UserSettings.All.GifskiLocation, "gifski_drop");
            }
            else
            {
                //Newer versions.
                _setFileOutput = (SetFileOutputDelegate)FunctionLoader.LoadFunction<SetFileOutputDelegate>(UserSettings.All.GifskiLocation, "gifski_set_file_output");
                _finish = (FinishDelegate)FunctionLoader.LoadFunction<FinishDelegate>(UserSettings.All.GifskiLocation, "gifski_finish");
            }

            #endregion
        }

        internal IntPtr Start(int quality, bool looped = true, bool fast = false)
        {
            return _new(new GifskiSettings((byte)quality, looped, fast));
        }

        internal GifskiError AddFrame(IntPtr handle, uint index, string path, int delay)
        {
            //if (Version > 0)
            //{
            //    var util = new PixelUtil(path.SourceFrom());
            //    util.LockBits();

            //    var result = AddFramePixels(handle, index, (uint)util.Width, (uint)util.Height, util.BackBuffer, delay);

            //    util.UnlockBitsWithoutCommit();

            //    return result;
            //}

            return _addPngFrame(handle, index, path, (ushort)(delay / 10));
        }

        internal GifskiError AddFramePixels(IntPtr handle, uint index, uint width, uint height, IntPtr pixels, int delay)
        {
            return _addRgbaFrame(handle, index, width, height, pixels, (ushort)(delay / 10));
        }

        internal GifskiError EndAdding(IntPtr handle)
        {
            if (Version.Major == 0 && Version.Minor < 9)
                return _endAddingFrames(handle);

            return _finish(handle);
        }

        internal GifskiError SetOutput(IntPtr handle, string destination)
        {
            return _setFileOutput(handle, destination);
        }

        internal GifskiError End(IntPtr handle, string destination)
        {
            var status = _write(handle, destination);

            if (status != GifskiError.Ok)
            {
                _drop(handle);
                return status;
            }

            _drop(handle);
            return GifskiError.Ok;
        }
    }
}