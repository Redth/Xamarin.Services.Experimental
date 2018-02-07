﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AVFoundation;
using UIKit;
using Xamarin.Services;

namespace Xamarin.Services.TextToSpeech
{
	public class TextToSpeechService
#if !EXCLUDE_INTERFACES
		: ITextToSpeechService
#endif
	{
		readonly AVSpeechSynthesizer speechSynthesizer;
		readonly SemaphoreSlim semaphore;

		public TextToSpeechService()
		{
			speechSynthesizer = new AVSpeechSynthesizer();
			semaphore = new SemaphoreSlim(1, 1);
		}

		public async Task SpeakAsync(string text, Locale? locale = null, float? pitch = null, float? speakRate = null, float? volume = null, CancellationToken cancelToken = default(CancellationToken))
		{
			if (text == null)
				throw new ArgumentNullException(nameof(text), "Text can not be null");

			try
			{
				await semaphore.WaitAsync(cancelToken);
				var speechUtterance = GetSpeechUtterance(text, locale, pitch, speakRate, volume);
				await SpeakUtterance(speechUtterance, cancelToken);
			}
			finally
			{
				if (semaphore.CurrentCount == 0)
					semaphore.Release();
			}
		}

		public Task<IEnumerable<Locale>> GetInstalledLanguagesAsync() =>
			Task.FromResult(AVSpeechSynthesisVoice.GetSpeechVoices()
			  .OrderBy(a => a.Language)
			  .Select(a => new Locale { Language = a.Language, DisplayName = a.Language }));

		private AVSpeechUtterance GetSpeechUtterance(string text, Locale? locale, float? pitch, float? speakRate, float? volume)
		{
			AVSpeechUtterance speechUtterance;

			var voice = GetVoiceForLocaleLanguage(locale);

			speakRate = NormalizeSpeakRate(speakRate);
			volume = NormalizeVolume(volume);
			pitch = NormalizePitch(pitch);

			speechUtterance = new AVSpeechUtterance(text)
			{
				Rate = speakRate.Value,
				Voice = voice,
				Volume = volume.Value,
				PitchMultiplier = pitch.Value
			};

			return speechUtterance;
		}

		private AVSpeechSynthesisVoice GetVoiceForLocaleLanguage(Locale? crossLocale)
		{
			var localCode = crossLocale.HasValue &&
										!string.IsNullOrWhiteSpace(crossLocale.Value.Language) ?
										crossLocale.Value.Language :
										AVSpeechSynthesisVoice.CurrentLanguageCode;

			var voice = AVSpeechSynthesisVoice.FromLanguage(localCode);
			if (voice == null)
			{
				Console.WriteLine("Locale not found for voice: " + localCode + " is not valid. Using default.");
				voice = AVSpeechSynthesisVoice.FromLanguage(AVSpeechSynthesisVoice.CurrentLanguageCode);
			}

			return voice;
		}

		private float? NormalizeSpeakRate(float? speakRate)
		{
			var divideBy = 4.0f;
			if (UIDevice.CurrentDevice.CheckSystemVersion(9, 0)) //use default .5f
				divideBy = 2.0f;
			else if (UIDevice.CurrentDevice.CheckSystemVersion(8, 0)) //use .125f
				divideBy = 8.0f;
			else
				divideBy = 4.0f; //use .25f

			if (!speakRate.HasValue)
				speakRate = AVSpeechUtterance.MaximumSpeechRate / divideBy; //normal speech, default is fast
			else if (speakRate.Value > AVSpeechUtterance.MaximumSpeechRate)
				speakRate = AVSpeechUtterance.MaximumSpeechRate;
			else if (speakRate.Value < AVSpeechUtterance.MinimumSpeechRate)
				speakRate = AVSpeechUtterance.MinimumSpeechRate;

			return speakRate;
		}

		private static float? NormalizeVolume(float? volume)
		{
			if (!volume.HasValue)
				volume = 1.0f;
			else if (volume > 1.0f)
				volume = 1.0f;
			else if (volume < 0.0f)
				volume = 0.0f;

			return volume;
		}

		private static float? NormalizePitch(float? pitch) =>
			pitch.GetValueOrDefault(1.0f);

		TaskCompletionSource<object> currentSpeak;
		async Task SpeakUtterance(AVSpeechUtterance speechUtterance, CancellationToken cancelToken)
		{
			try
			{
				currentSpeak = new TaskCompletionSource<object>();

				speechSynthesizer.DidFinishSpeechUtterance += OnFinishedSpeechUtterance;
				speechSynthesizer.SpeakUtterance(speechUtterance);
				using (cancelToken.Register(TryCancel))
				{
					await currentSpeak.Task;
				}
			}
			finally
			{
				speechSynthesizer.DidFinishSpeechUtterance -= OnFinishedSpeechUtterance;
			}
		}

		void OnFinishedSpeechUtterance(object sender, AVSpeechSynthesizerUteranceEventArgs args) =>
			currentSpeak?.TrySetResult(null);

		void TryCancel()
		{
			speechSynthesizer?.StopSpeaking(AVSpeechBoundary.Word);
			currentSpeak?.TrySetCanceled();
		}

		public int MaxSpeechInputLength => -1;

		public void Dispose() => speechSynthesizer?.Dispose();
	}
}
