using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace Totoro.Modules.MediaPlayers.MediaFoundation;

/// <summary>
/// A subtitle track of a Matroska file: its number in the file, language and name (empty if it has none), whether it's
/// one the file would have shown by default, and whether it's forced (only for what isn't in the audio's language, such
/// as signs).
/// </summary>
internal sealed record MatroskaSubtitleTrack(ulong Number, string Language, string Name, bool IsDefault, bool IsForced);

/// <summary>A subtitle: its track, when it shows and for how long, and its text (lines split by '\n').</summary>
internal readonly record struct MatroskaCue(ulong Track, TimeSpan Start, TimeSpan Duration, string Text);

/// <summary>What a Matroska file's header says, shared by the parsers reading it on from different places.</summary>
internal sealed class MatroskaHeader
{
	/// <summary>Nanoseconds per unit of the file's timestamps.</summary>
	public long TimestampScale { get; set; } = 1_000_000;

	/// <summary>The subtitle tracks the parser can read, by number.</summary>
	public Dictionary<ulong, MatroskaSubtitleCodec> Codecs { get; } = [];

	/// <summary>Its subtitle tracks, once its header's been read.</summary>
	public TaskCompletionSource<IReadOnlyList<MatroskaSubtitleTrack>> Tracks { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <param name="IsAss">ASS or SSA rather than plain text (SRT).</param>
/// <param name="Compression">How each block is compressed: null if it isn't, 0 zlib, 3 its first bytes taken off.</param>
/// <param name="Stripped">The bytes taken off each block, for <paramref name="Compression"/> 3.</param>
internal sealed record MatroskaSubtitleCodec(bool IsAss, ulong? Compression, byte[] Stripped);

/// <summary>
/// Reads the subtitles out of a Matroska file as it's downloaded from some offset, given its bytes in order: from the
/// start, its header (its tracks) too; from elsewhere, from its next cluster on. Only subtitle blocks (and the header) are
/// kept to be read; the video and audio are skipped as they come.
/// </summary>
internal sealed partial class MatroskaSubtitleParser
{
	private const uint SegmentId = 0x18538067;
	private const uint InfoId = 0x1549A966;
	private const uint TracksId = 0x1654AE6B;
	private const uint ClusterId = 0x1F43B675;
	private const uint TimestampId = 0xE7;
	private const uint SimpleBlockId = 0xA3;
	private const uint BlockGroupId = 0xA0;
	private const uint BlockId = 0xA1;
	private const uint BlockDurationId = 0x9B;
	// An element's ID and size take at most this many bytes.
	private const int MaxHeader = 12;
	// The most kept of one element (a subtitle block, the header's tracks): anything bigger is skipped.
	private const int MaxElement = 4 * 1024 * 1024;
	// shortcut: a subtitle without a duration (Matroska's text subtitles should have one) shows this long.
	private static readonly TimeSpan s_defaultDuration = TimeSpan.FromSeconds(3);

	private readonly MatroskaHeader _header;
	private readonly bool _isFromStart;
	private byte[] _buffer = new byte[64 * 1024];
	// What's kept is the file's bytes [_bufferAt, _received).
	private long _bufferAt;
	private long _received;
	// Where the next element starts.
	private long _next;
	private bool _isSynced;
	// While in a cluster, where it ends (long.MaxValue if its size isn't known); -1 outside one.
	private long _clusterEnd = -1;
	private long _clusterTime;

	/// <param name="at">Where in the file the bytes it's given start.</param>
	public MatroskaSubtitleParser(MatroskaHeader header, long at)
	{
		_header = header;
		_bufferAt = _received = _next = at;
		_isSynced = _isFromStart = at == 0;
	}

	/// <summary>Reads on with the file's next bytes, adding the subtitles they finish to <paramref name="cues"/>.</summary>
	public void Feed(ReadOnlySpan<byte> data, List<MatroskaCue> cues)
	{
		// What's before the next element (what's skipped) isn't kept.
		if (_received < _next)
		{
			var skip = (int)Math.Min(data.Length, _next - _received);
			data = data[skip..];
			_received += skip;
			_bufferAt = _received;
		}

		if (data.Length > 0)
		{
			var kept = (int)(_received - _bufferAt);
			if (kept + data.Length > _buffer.Length)
			{
				Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, kept + data.Length));
			}

			data.CopyTo(_buffer.AsSpan(kept));
			_received += data.Length;
		}

		while (_next < _received && Step(cues))
		{
		}

		// What's been read isn't kept.
		var used = (int)(Math.Min(_next, _received) - _bufferAt);
		if (used > 0)
		{
			_buffer.AsSpan(used, (int)(_received - _bufferAt) - used).CopyTo(_buffer);
			_bufferAt += used;
		}
	}

	/// <summary>Reads the next element (or skips it); false if it needs more of the file first.</summary>
	private bool Step(List<MatroskaCue> cues)
	{
		var data = _buffer.AsSpan((int)(_next - _bufferAt), (int)(_received - _next));
		if (!_isSynced)
		{
			return Sync(data);
		}

		if (!TryReadHeader(data, out var id, out var size, out var headerLength))
		{
			if (data.Length < MaxHeader)
			{
				return false;
			}

			// Not an element where one was expected: it's found again from a cluster.
			_isSynced = false;
			return true;
		}

		var end = size is { } known ? _next + headerLength + (long)known : long.MaxValue;
		if (_clusterEnd >= 0 && (_next >= _clusterEnd || id == ClusterId))
		{
			_clusterEnd = -1;
		}

		if (_clusterEnd < 0)
		{
			switch (id)
			{
				case SegmentId:
					_next += headerLength;
					return true;
				case ClusterId:
					// The header comes before the clusters: read from the start, there's none if it hasn't been by now.
					if (_isFromStart)
					{
						_header.Tracks.TrySetResult([]);
					}

					_clusterEnd = end;
					_next += headerLength;
					return true;
				case InfoId or TracksId when !_header.Tracks.Task.IsCompleted && size <= MaxElement:
					if (data.Length < headerLength + (int)size)
					{
						return false;
					}

					var payload = data.Slice(headerLength, (int)size);
					if (id == InfoId)
					{
						ReadInfo(payload);
					}
					else
					{
						_header.Tracks.TrySetResult(ReadTracks(payload));
					}

					_next = end;
					return true;
				default:
					return Skip(end);
			}
		}

		switch (id)
		{
			case TimestampId when size <= 8:
				if (data.Length < headerLength + (int)size)
				{
					return false;
				}

				_clusterTime = (long)ReadUInt(data.Slice(headerLength, (int)size));
				_next = end;
				return true;
			case SimpleBlockId or BlockGroupId when size is not null:
				return ReadBlock(data, id, (int)Math.Min((long)size.Value, int.MaxValue), headerLength, end, cues);
			default:
				return Skip(end);
		}
	}

	/// <summary>Finds the next cluster: its ID, then a size, then its timestamp's ID (after a CRC-32, as ffmpeg writes).</summary>
	private bool Sync(ReadOnlySpan<byte> data)
	{
		ReadOnlySpan<byte> clusterId = [0x1F, 0x43, 0xB6, 0x75];
		for (var i = data.IndexOf(clusterId); i >= 0; i = data[(i + 1)..].IndexOf(clusterId) is >= 0 and var j ? i + 1 + j : -1)
		{
			var rest = data[(i + 4)..];
			if (rest.Length < 15)
			{
				// Not enough yet to tell.
				_next += i;
				return false;
			}

			// CRC-32: its ID, its size (4) and the 4 bytes.
			if (TryReadSize(rest, out _, out var sizeLength) && rest[sizeLength..] is var child &&
				(child[0] == TimestampId || (child[0] == 0xBF && child[1] == 0x84 && child[6] == TimestampId)))
			{
				_next += i;
				_isSynced = true;
				_clusterEnd = -1;
				return true;
			}
		}

		// The ID may yet start in the last few bytes.
		_next += Math.Max(0, data.Length - 3);
		return false;
	}

	private bool Skip(long end)
	{
		if (end == long.MaxValue)
		{
			// An element of unknown size can't be skipped: it's found again from a cluster.
			_isSynced = false;
			return true;
		}

		_next = end;
		return true;
	}

	/// <summary>A block, read if it's of a subtitle track (as far as its track's number, to tell), skipped if not.</summary>
	private bool ReadBlock(ReadOnlySpan<byte> data, uint id, int size, int headerLength, long end, List<MatroskaCue> cues)
	{
		var blockAt = headerLength;
		if (id == BlockGroupId)
		{
			// Its Block comes first, as a rule.
			if (!TryReadHeader(data[headerLength..], out var childId, out _, out var childHeader))
			{
				return data.Length < headerLength + MaxHeader ? false : Skip(end);
			}

			if (childId != BlockId)
			{
				return Skip(end);
			}

			blockAt += childHeader;
		}

		if (!TryReadSize(data[Math.Min(blockAt, data.Length)..], out var track, out _))
		{
			return data.Length < blockAt + 8 && data.Length < headerLength + size ? false : Skip(end);
		}

		if (!_header.Codecs.TryGetValue(track ?? 0, out var codec) || size > MaxElement)
		{
			return Skip(end);
		}

		if (data.Length < headerLength + size)
		{
			return false;
		}

		var element = data.Slice(headerLength, size);
		ReadOnlySpan<byte> block = default;
		ulong? duration = null;
		if (id == SimpleBlockId)
		{
			block = element;
		}
		else
		{
			for (var at = 0; at < element.Length && TryReadHeader(element[at..], out var childId, out var childSize, out var childHeader) && childSize is { } length; at += childHeader + (int)length)
			{
				var child = element.Slice(at + childHeader, (int)Math.Min((long)length, element.Length - at - childHeader));
				if (childId == BlockId)
				{
					block = child;
				}
				else if (childId == BlockDurationId)
				{
					duration = ReadUInt(child);
				}
			}
		}

		if (ReadCue(block, codec, duration) is { } cue)
		{
			cues.Add(cue);
		}

		_next = end;
		return true;
	}

	private MatroskaCue? ReadCue(ReadOnlySpan<byte> block, MatroskaSubtitleCodec codec, ulong? duration)
	{
		if (!TryReadSize(block, out var track, out var trackLength) || block.Length < trackLength + 3 ||
			// Laced: several in a block, as subtitles never are.
			(block[trackLength + 2] & 0x06) != 0)
		{
			return null;
		}

		var relative = BinaryPrimitives.ReadInt16BigEndian(block[trackLength..]);
		var payload = block[(trackLength + 3)..];
		byte[] bytes;
		try
		{
			bytes = codec.Compression switch
			{
				null => payload.ToArray(),
				0 => Inflate(payload),
				3 => [.. codec.Stripped, .. payload],
				_ => [],
			};
		}
		catch (InvalidDataException)
		{
			return null;
		}

		var text = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
		text = codec.IsAss ? AssText(text) : Tag().Replace(text, "");
		text = text.Replace("\r\n", "\n").Trim();
		if (text.Length == 0)
		{
			return null;
		}

		var scale = _header.TimestampScale;
		var start = TimeSpan.FromTicks((_clusterTime + relative) * scale / 100);
		var length = duration is { } d ? TimeSpan.FromTicks((long)d * scale / 100) : s_defaultDuration;
		return new MatroskaCue(track!.Value, start, length, text);
	}

	private void ReadInfo(ReadOnlySpan<byte> info)
	{
		foreach (var (id, payload) in Children(info))
		{
			// TimestampScale
			if (id == 0x2AD7B1 && ReadUInt(payload) is > 0 and var scale)
			{
				_header.TimestampScale = (long)scale;
			}
		}
	}

	private List<MatroskaSubtitleTrack> ReadTracks(ReadOnlySpan<byte> tracks)
	{
		List<MatroskaSubtitleTrack> found = [];
		foreach (var (entryId, entry) in Children(tracks))
		{
			// TrackEntry
			if (entryId != 0xAE)
			{
				continue;
			}

			// A track is a default one unless it says not.
			ulong number = 0, type = 0, isDefault = 1, forced = 0;
			string codecId = "", language = "eng", bcp47 = "", name = "";
			var isEncoded = false;
			ulong? compression = null;
			byte[] stripped = [];
			foreach (var (id, value) in Children(entry))
			{
				switch (id)
				{
					case 0xD7:
						number = ReadUInt(value);
						break;
					case 0x83:
						type = ReadUInt(value);
						break;
					case 0x86:
						codecId = ReadString(value);
						break;
					case 0x22B59C:
						language = ReadString(value);
						break;
					case 0x22B59D:
						bcp47 = ReadString(value);
						break;
					case 0x536E:
						name = ReadString(value);
						break;
					case 0x88:
						isDefault = ReadUInt(value);
						break;
					case 0x55AA:
						forced = ReadUInt(value);
						break;
					case 0x6D80:
						isEncoded = true;
						(compression, stripped) = ReadCompression(value);
						break;
				}
			}

			// A subtitle track (17) of text, readable: compressed (zlib, or its first bytes taken off) but not encrypted.
			var isAss = codecId is "S_TEXT/ASS" or "S_TEXT/SSA";
			if (type != 17 || number == 0 || !(isAss || codecId == "S_TEXT/UTF8") || (isEncoded && compression is not (0 or 3)))
			{
				continue;
			}

			_header.Codecs[number] = new MatroskaSubtitleCodec(isAss, compression, stripped);
			found.Add(new MatroskaSubtitleTrack(number, bcp47.Length > 0 ? bcp47 : language, name, isDefault != 0, forced != 0));
		}

		return found;
	}

	/// <summary>ContentEncodings: its compression (zlib unless it says), or null if it's encrypted rather than compressed.</summary>
	private static (ulong? Algorithm, byte[] Stripped) ReadCompression(ReadOnlySpan<byte> encodings)
	{
		foreach (var (encodingId, encoding) in Children(encodings))
		{
			// ContentEncoding
			if (encodingId != 0x6240)
			{
				continue;
			}

			foreach (var (id, compression) in Children(encoding))
			{
				// ContentCompression
				if (id != 0x5034)
				{
					continue;
				}

				ulong algorithm = 0;
				byte[] stripped = [];
				foreach (var (settingId, value) in Children(compression))
				{
					if (settingId == 0x4254)
					{
						algorithm = ReadUInt(value);
					}
					else if (settingId == 0x4255)
					{
						stripped = value.ToArray();
					}
				}

				return (algorithm, stripped);
			}
		}

		return (null, []);
	}

	private static List<(uint Id, byte[] Payload)> Children(ReadOnlySpan<byte> data)
	{
		List<(uint, byte[])> children = [];
		for (var at = 0; at < data.Length && TryReadHeader(data[at..], out var id, out var size, out var headerLength) && size is { } length && at + headerLength + (long)length <= data.Length; at += headerLength + (int)length)
		{
			children.Add((id, data.Slice(at + headerLength, (int)length).ToArray()));
		}

		return children;
	}

	/// <summary>An element's ID and size (null if it isn't known); false if there aren't enough bytes for them, or they're not valid.</summary>
	internal static bool TryReadHeader(ReadOnlySpan<byte> data, out uint id, out ulong? size, out int length)
	{
		(id, size, length) = (0, null, 0);
		if (data.IsEmpty || data[0] == 0)
		{
			return false;
		}

		var idLength = BitOperations.LeadingZeroCount((uint)data[0]) - 23;
		if (idLength > 4 || data.Length < idLength)
		{
			return false;
		}

		for (var i = 0; i < idLength; i++)
		{
			id = id << 8 | data[i];
		}

		if (!TryReadSize(data[idLength..], out size, out var sizeLength))
		{
			return false;
		}

		length = idLength + sizeLength;
		return true;
	}

	/// <summary>A variable-length number (null if all its bits are set: unknown).</summary>
	private static bool TryReadSize(ReadOnlySpan<byte> data, out ulong? value, out int length)
	{
		(value, length) = (null, 0);
		if (data.IsEmpty || data[0] == 0)
		{
			return false;
		}

		length = BitOperations.LeadingZeroCount((uint)data[0]) - 23;
		if (data.Length < length)
		{
			return false;
		}

		var number = (ulong)(data[0] & (0xFF >> length));
		var isUnknown = number == (ulong)(0xFF >> length);
		for (var i = 1; i < length; i++)
		{
			number = number << 8 | data[i];
			isUnknown &= data[i] == 0xFF;
		}

		value = isUnknown ? null : number;
		return true;
	}

	private static ulong ReadUInt(ReadOnlySpan<byte> data)
	{
		ulong value = 0;
		foreach (var b in data[..Math.Min(data.Length, 8)])
		{
			value = value << 8 | b;
		}

		return value;
	}

	private static string ReadString(ReadOnlySpan<byte> data) => Encoding.UTF8.GetString(data).TrimEnd('\0');

	private static byte[] Inflate(ReadOnlySpan<byte> data)
	{
		using var inflated = new MemoryStream();
		using (var zlib = new ZLibStream(new MemoryStream(data.ToArray()), CompressionMode.Decompress))
		{
			zlib.CopyTo(inflated);
		}

		return inflated.ToArray();
	}

	/// <summary>
	/// An ASS block's text (its 9th field: ReadOrder, Layer, Style, Name, MarginL, MarginR, MarginV, Effect, Text), its line
	/// breaks kept but not its styling.
	/// </summary>
	/// shortcut: ASS styling (positions, colours, fonts) is dropped, as Windows shows subtitles its own way.
	private static string AssText(string block)
	{
		var text = block.Split(',', 9) is { Length: 9 } fields ? fields[8] : block;
		return AssOverride().Replace(text, "").Replace("\\N", "\n").Replace("\\n", "\n").Replace("\\h", " ");
	}

	[GeneratedRegex("<[^>]*>")]
	private static partial Regex Tag();

	[GeneratedRegex(@"\{[^}]*\}")]
	private static partial Regex AssOverride();
}
