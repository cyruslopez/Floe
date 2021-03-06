﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Floe.Audio.Interop;

namespace Floe.Audio
{
	public class AudioOutputClient : AudioClient
	{
		private IAudioRenderClient _render;

		internal AudioOutputClient(IAudioClient client, WaveFormat format)
			: base(client, format)
		{
			_render = this.GetService<IAudioRenderClient>();
		}

		protected override void Loop(object state)
		{
			var args = (LoopArgs)state;

			var handles = new[] { this.BufferEvent, args.CancelToken.WaitHandle };
			if (!this.ReadBuffer(args.Stream, this.BufferSize))
			{
				return;
			}
			this.Client.Start();
			bool done = false;

			try
			{
				while (true)
				{
					switch (WaitHandle.WaitAny(handles))
					{
						case 0:
							if (done)
							{
								return;
							}
							int padding;
							this.Client.GetCurrentPadding(out padding);
							done = !this.ReadBuffer(args.Stream, this.BufferSize - padding);
							break;
						case 1:
							return;
					}
				}
			}
			finally
			{
				this.Client.Stop();
			}
		}

		private bool ReadBuffer(Stream stream, int count)
		{
			int total = 0, remainder = 0, bytes;

			do
			{
				bytes = stream.Read(this.Buffer, total, remainder > 0 ? remainder : count * this.FrameSize);
				total += bytes;
				remainder = remainder > 0 ? (remainder -= bytes) : total % this.FrameSize;
			}
			while (remainder != 0 && bytes > 0);

			if (total == 0)
			{
				return false;
			}

			IntPtr p;
			_render.GetBuffer(count, out p);
			Marshal.Copy(this.Buffer, 0, p, bytes);
			_render.ReleaseBuffer(bytes / this.FrameSize, AudioClientBufferFlags.None);
			return true;
		}
	}
}
