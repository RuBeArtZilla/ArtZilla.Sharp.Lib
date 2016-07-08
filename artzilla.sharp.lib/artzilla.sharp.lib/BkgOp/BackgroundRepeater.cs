﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ArtZilla.Sharp.Lib {
	/// <summary> Represent a background repeated action </summary>
	public class BackgroundRepeater {
		/// <summary> Default value of <see cref="Cooldown"/> in milliseconds </summary>
		public const Double DefaultCooldownMs = 1000D;

		/// <summary> Default value of <see cref="IsCatchExceptions"/></summary>
		public const Boolean DefaultIsCatchExceptions = true;

		/// <summary> Period between repeating background operation </summary>
		public TimeSpan Cooldown { get; set; } = TimeSpan.FromMilliseconds(DefaultCooldownMs);

		/// <summary> When true any exception from repeated operation will be ignored </summary>
		public Boolean IsCatchExceptions { get; set; } = DefaultIsCatchExceptions;

		/// <summary> Start repeating </summary>
		public void Start() {
			Debug.Assert(_sync != null, "_sync != null");

			lock (_sync) {
				if (_cts != null)
					return;

				_cts = new CancellationTokenSource();
				_thread = new Thread(o => Repeater(_action, o)) {
					IsBackground = true,
					Name = nameof(BackgroundRepeater),
				};

				_thread?.Start(_cts.Token);
			}
		}

		/// <summary> Stop repeating </summary>
		public void Stop() {
			Debug.Assert(_sync != null, "_sync != null");

			lock (_sync) {
				if (_cts == null)
					return;

				try {
					_cts?.Cancel();
					_thread?.Join();
				} finally {
					_cts = null;
					_thread = null;
				}
			}
		}

		/// <summary> Gets a value indicating the execution status of current <see cref="BackgroundRepeater"/> </summary>
		public Boolean IsStarted() {
			Debug.Assert(_sync != null, "_sync != null");

			lock (_sync)
				return _cts != null;
		}

		/// <summary> Initializes a new <see cref="BackgroundRepeater"/> with specified action to repeat. </summary>
		/// <exception cref="ArgumentNullException">The <paramref name="action"/> argument is null.</exception>
		/// <param name="action">The delegate that represents the code to repeat.</param>
		public BackgroundRepeater(Action action) : this(t => Cancelable(action, t)) {
			if (action == null) throw new ArgumentNullException();
		}

		/// <summary> Initializes a new <see cref="BackgroundRepeater"/> with specified cancellable action to repeat. </summary>
		/// <exception cref="ArgumentNullException">The <paramref name="action"/> argument is null.</exception>
		/// <param name="action">The delegate that represents the code to repeat.</param>
		public BackgroundRepeater(Action<CancellationToken> action) {
			if (action == null) throw new ArgumentNullException();
			_action = action;
		}

		private static void Cancelable(Action action, CancellationToken token) {
			using (var t = new Task(action, token)) {
				t.Start();
				t.Wait(token);
			}
		}

		private void Repeater(Action<CancellationToken> action, Object token) {
			Debug.Assert(token is CancellationToken, "Wrong cancellation token.");
			var t = (CancellationToken) token;

			try {
				while (!t.IsCancellationRequested) {
					if (IsCatchExceptions)
						try {
							action?.Invoke(t);
						} catch {
							// ignored
						} else
						action?.Invoke(t);

					if (!t.IsCancellationRequested)
						t.WaitHandle.WaitOne(Cooldown);  // Thread.Sleep(Cooldown);
				}
			} catch (OperationCanceledException) {
				// ignored, worked in rare cases!
			} finally {
				_cts = null;
				_thread = null;
			}
		}

		private Thread _thread;
		private CancellationTokenSource _cts;
		private readonly Object _sync = new Object();
		private readonly Action<CancellationToken> _action;
	}
}