#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.IO;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA
{
	sealed class HeadlessPlatform : IPlatform
	{
		public IPlatformWindow CreateWindow(
			Size size,
			WindowMode windowMode,
			float scaleModifier,
			int vertexBatchSize,
			int indexBatchSize,
			int videoDisplay,
			GLProfile profile)
		{
			return new HeadlessPlatformWindow(size, scaleModifier);
		}

		public ISoundEngine CreateSound(string device) { return new HeadlessSoundEngine(); }
		public IFont CreateFont(byte[] data) { return new HeadlessFont(); }
	}

	sealed class HeadlessPlatformWindow : IPlatformWindow
	{
		Size size;
		float scale;

		public HeadlessPlatformWindow(Size size, float scale)
		{
			this.size = size;
			this.scale = scale;
		}

		public IGraphicsContext Context { get; } = new HeadlessGraphicsContext();
		public Size NativeWindowSize => size;
		public Size EffectiveWindowSize => size;
		public float NativeWindowScale => scale;
		public float EffectiveWindowScale => scale;
		public Size SurfaceSize => size;
		public int DisplayCount => 1;
		public int CurrentDisplay => 0;
		public bool HasInputFocus => false;
		public bool IsSuspended => false;
		public event Action<float, float, float, float> OnWindowScaleChanged = (_, _, _, _) => { };
		public GLProfile GLProfile => GLProfile.Automatic;
		public GLProfile[] SupportedGLProfiles => [GLProfile.Automatic];

		public void PumpInput(IInputHandler inputHandler) { }
		public string GetClipboardText() { return ""; }
		public bool SetClipboardText(string text) { return false; }
		public bool TryOpenUrl(string url) { return false; }
		public void GrabWindowMouseFocus() { }
		public void ReleaseWindowMouseFocus() { }
		public IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble)
		{
			return new HeadlessHardwareCursor();
		}

		public void SetHardwareCursor(IHardwareCursor cursor) { }
		public void SetWindowTitle(string title) { }
		public void SetRelativeMouseMode(bool mode) { }
		public void SetScaleModifier(float scale)
		{
			var previous = this.scale;
			this.scale = scale;
			OnWindowScaleChanged(previous, previous, scale, scale);
		}

		public void Dispose() { Context.Dispose(); }
	}

	sealed class HeadlessGraphicsContext : IGraphicsContext
	{
		public string GLVersion => "Headless";
		public IVertexBuffer<T> CreateEmptyVertexBuffer<T>(int size) where T : struct { return new HeadlessVertexBuffer<T>(); }
		public IVertexBuffer<T> CreateVertexBuffer<T>(T[] data, bool dynamic = true) where T : struct
		{
			return new HeadlessVertexBuffer<T>();
		}

		public T[] CreateVertices<T>(int size) where T : struct { return new T[size]; }
		public IIndexBuffer CreateIndexBuffer(uint[] indices) { return new HeadlessIndexBuffer(); }
		public ITexture CreateTexture() { return new HeadlessTexture(); }
		public IFrameBuffer CreateFrameBuffer(Size size) { return new HeadlessFrameBuffer(size); }
		public IFrameBuffer CreateFrameBuffer(Size size, Color clearColor) { return new HeadlessFrameBuffer(size); }
		public IShader CreateShader(IShaderBindings shaderBindings) { return new HeadlessShader(); }
		public void EnableScissor(int x, int y, int width, int height) { }
		public void DisableScissor() { }
		public void Present() { }
		public void DrawPrimitives(PrimitiveType primitiveType, int firstVertex, int numVertices) { }
		public void DrawElements(int numIndices, int offset) { }
		public void Clear() { }
		public void EnableDepthBuffer() { }
		public void DisableDepthBuffer() { }
		public void ClearDepthBuffer() { }
		public void SetBlendMode(BlendMode mode) { }
		public void SetVSyncEnabled(bool enabled) { }
		public void Dispose() { }
	}

	sealed class HeadlessVertexBuffer<T> : IVertexBuffer<T> where T : struct
	{
		public void Bind() { }
		public void SetData(T[] vertices, int length) { }
		public void SetData(ref T[] vertices, int length) { }
		public void SetData(T[] vertices, int offset, int start, int length) { }
		public void Dispose() { }
	}

	sealed class HeadlessIndexBuffer : IIndexBuffer
	{
		public void Bind() { }
		public void Dispose() { }
	}

	sealed class HeadlessTexture : ITexture
	{
		byte[] data = [];

		public Size Size { get; private set; }
		public TextureScaleFilter ScaleFilter { get; set; }

		public void SetData(byte[] colors, int width, int height)
		{
			data = colors;
			Size = new Size(width, height);
		}

		public void SetFloatData(float[] values, int width, int height)
		{
			data = new byte[values.Length * sizeof(float)];
			Buffer.BlockCopy(values, 0, data, 0, data.Length);
			Size = new Size(width, height);
		}

		public void SetDataFromReadBuffer(Rectangle rectangle)
		{
			Size = new Size(rectangle.Width, rectangle.Height);
			data = new byte[Math.Max(0, rectangle.Width * rectangle.Height * 4)];
		}

		public byte[] GetData() { return data; }
		public void Dispose() { }
	}

	sealed class HeadlessFrameBuffer : IFrameBuffer
	{
		public HeadlessFrameBuffer(Size size)
		{
			Texture = new HeadlessTexture();
			Texture.SetData(new byte[Math.Max(0, size.Width * size.Height * 4)], size.Width, size.Height);
		}

		public ITexture Texture { get; }
		public void Bind() { }
		public void Unbind() { }
		public void EnableScissor(Rectangle rectangle) { }
		public void DisableScissor() { }
		public void Dispose() { Texture.Dispose(); }
	}

	sealed class HeadlessShader : IShader
	{
		public void SetBool(string name, bool value) { }
		public void SetVec(string name, float x) { }
		public void SetVec(string name, float x, float y) { }
		public void SetVec(string name, float x, float y, float z) { }
		public void SetVec(string name, ReadOnlyMemory<float> vector, int length) { }
		public void SetTexture(string param, ITexture texture) { }
		public void SetMatrix(string param, float[] matrix) { }
		public void PrepareRender() { }
		public void Bind() { }
	}

	sealed class HeadlessFont : IFont
	{
		public FontGlyph CreateGlyph(char character, int size, float deviceScale)
		{
			var height = Math.Max(1, (int)Math.Ceiling(size * deviceScale));
			var width = Math.Max(1, height / 2);
			return new FontGlyph
			{
				Offset = int2.Zero,
				Size = new Size(width, height),
				Advance = width,
				Data = new byte[width * height]
			};
		}

		public void Dispose() { }
	}

	sealed class HeadlessHardwareCursor : IHardwareCursor
	{
		public void Dispose() { }
	}

	sealed class HeadlessSoundEngine : ISoundEngine
	{
		public bool Dummy => true;
		public float Volume { get => 0; set { } }
		public SoundDevice[] AvailableDevices() { return [new SoundDevice(null, "Headless")]; }
		public ISoundSource AddSoundSourceFromMemory(byte[] data, int channels, int sampleBits, int sampleRate)
		{
			return new HeadlessSoundSource();
		}

		public ISound Play2D(ISoundSource sound, bool loop, bool relative, WPos position, float volume, bool attenuateVolume)
		{
			return new HeadlessSound();
		}

		public ISound Play2DStream(
			Stream stream,
			int channels,
			int sampleBits,
			int sampleRate,
			bool loop,
			bool relative,
			WPos position,
			float volume)
		{
			return new HeadlessSound();
		}

		public void PauseSound(ISound sound, bool paused) { }
		public void StopSound(ISound sound) { }
		public void SetAllSoundsPaused(bool paused) { }
		public void StopAllSounds() { }
		public void SetListenerPosition(WPos position) { }
		public void SetSoundVolume(float volume, ISound music, ISound video) { }
		public void SetSoundLooping(bool looping, ISound sound) { }
		public void SetSoundPosition(ISound sound, WPos position) { }
		public void Dispose() { }
	}

	sealed class HeadlessSoundSource : ISoundSource
	{
		public void Dispose() { }
	}

	sealed class HeadlessSound : ISound
	{
		public float Volume { get; set; }
		public float SeekPosition => 0;
		public bool Complete => true;
		public void SetPosition(WPos position) { }
	}
}
