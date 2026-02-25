namespace NuGroom
{
	/// <summary>
	/// Fluent wrapper around <see cref="Console"/> that simplifies colored output.
	/// Usage: <c>ConsoleWriter.Red().WriteLine("error").ResetColor();</c>
	/// The class is a singleton struct returned by every method so calls can be chained
	/// without allocations.
	/// </summary>
	internal readonly struct ConsoleWriter
	{
		/// <summary>
		/// Gets a <see cref="ConsoleWriter"/> instance to start a fluent chain.
		/// </summary>
		public static ConsoleWriter Out => default;

		// ── Color setters ───────────────────────────────────────────────

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.Red"/>.</summary>
		public ConsoleWriter Red() { Console.ForegroundColor = ConsoleColor.Red; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.Green"/>.</summary>
		public ConsoleWriter Green() { Console.ForegroundColor = ConsoleColor.Green; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.Yellow"/>.</summary>
		public ConsoleWriter Yellow() { Console.ForegroundColor = ConsoleColor.Yellow; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.Cyan"/>.</summary>
		public ConsoleWriter Cyan() { Console.ForegroundColor = ConsoleColor.Cyan; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.Gray"/>.</summary>
		public ConsoleWriter Gray() { Console.ForegroundColor = ConsoleColor.Gray; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.DarkGray"/>.</summary>
		public ConsoleWriter DarkGray() { Console.ForegroundColor = ConsoleColor.DarkGray; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.White"/>.</summary>
		public ConsoleWriter White() { Console.ForegroundColor = ConsoleColor.White; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.DarkRed"/>.</summary>
		public ConsoleWriter DarkRed() { Console.ForegroundColor = ConsoleColor.DarkRed; return this; }

		/// <summary>Sets the foreground color to <see cref="ConsoleColor.DarkYellow"/>.</summary>
		public ConsoleWriter DarkYellow() { Console.ForegroundColor = ConsoleColor.DarkYellow; return this; }

		/// <summary>Sets the foreground color to the specified <see cref="ConsoleColor"/>.</summary>
		/// <param name="color">The color to apply.</param>
		public ConsoleWriter Color(ConsoleColor color) { Console.ForegroundColor = color; return this; }

		/// <summary>Resets the foreground color to its default.</summary>
		public ConsoleWriter ResetColor() { Console.ResetColor(); return this; }

		// ── Write operations ────────────────────────────────────────────

		/// <summary>Writes <paramref name="text"/> to the console without a trailing newline.</summary>
		/// <param name="text">The text to write.</param>
		public ConsoleWriter Write(string text) { Console.Write(text); return this; }

		/// <summary>Writes <paramref name="text"/> followed by a newline to the console.</summary>
		/// <param name="text">The text to write.</param>
		public ConsoleWriter WriteLine(string text) { Console.WriteLine(text); return this; }

		/// <summary>Writes a blank line to the console.</summary>
		public ConsoleWriter WriteLine() { Console.WriteLine(); return this; }

		// ── Convenience shortcuts ───────────────────────────────────────

		/// <summary>
		/// Writes <paramref name="text"/> in the specified color, then resets the color.
		/// Equivalent to <c>.Color(color).WriteLine(text).ResetColor()</c>.
		/// </summary>
		/// <param name="color">The color to use.</param>
		/// <param name="text">The text to write.</param>
		public ConsoleWriter WriteLineColored(ConsoleColor color, string text)
		{
			Console.ForegroundColor = color;
			Console.WriteLine(text);
			Console.ResetColor();

			return this;
		}

		/// <summary>
		/// Writes <paramref name="text"/> in the specified color without a trailing newline, then resets the color.
		/// Equivalent to <c>.Color(color).Write(text).ResetColor()</c>.
		/// </summary>
		/// <param name="color">The color to use.</param>
		/// <param name="text">The text to write.</param>
		public ConsoleWriter WriteColored(ConsoleColor color, string text)
		{
			Console.ForegroundColor = color;
			Console.Write(text);
			Console.ResetColor();

			return this;
		}
	}
}
