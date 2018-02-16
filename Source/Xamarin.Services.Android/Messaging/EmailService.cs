using System;
using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.Content;
using Android.Text;
using Java.IO;
using Uri = Android.Net.Uri;

namespace Xamarin.Services.Messaging
{
	public partial class EmailService
	{
		private readonly EmailSettings settings;

		public EmailService()
		{
			settings = new EmailSettings();
		}

		public EmailService(EmailSettings settings)
		{
			this.settings = settings;
		}

		public bool CanSendEmail
		{
			get
			{
				var emailIntent = new Intent(Intent.ActionSend);
				emailIntent.SetType("message/rfc822");
				return CanSend(emailIntent);
			}
		}

		public bool CanSendEmailAttachments => true;

		public bool CanSendEmailBodyAsHtml => true;

		public void SendEmail(EmailMessage email)
		{
			// NOTE: http://developer.xamarin.com/recipes/android/networking/email/send_an_email/

			if (email == null)
				throw new ArgumentNullException(nameof(email));

			// NOTE: http://stackoverflow.com/questions/15946297/sending-email-with-attachment-using-sendto-on-some-devices-doesnt-work
			if (settings.UseStrictMode && email.Attachments.Count > 0)
				throw new NotSupportedException("Cannot use StrictMode when sending attachments");

			var emailIntent = ResolveSendIntent(email);

			if (CanSend(emailIntent))
			{
				// NOTE: http://developer.android.com/guide/components/intents-common.html#Email

				if (email.Recipients.Count > 0)
					emailIntent.PutExtra(Intent.ExtraEmail, email.Recipients.ToArray());

				if (email.RecipientsCc.Count > 0)
					emailIntent.PutExtra(Intent.ExtraCc, email.RecipientsCc.ToArray());

				if (email.RecipientsBcc.Count > 0)
					emailIntent.PutExtra(Intent.ExtraBcc, email.RecipientsBcc.ToArray());

				emailIntent.PutExtra(Intent.ExtraSubject, email.Subject);

				// NOTE: http://stackoverflow.com/questions/13756200/send-html-email-with-gmail-4-2-1

				if (email.IsHtml)
				{
					ISpanned html;
					if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
					{
						html = Html.FromHtml(email.Message, FromHtmlOptions.ModeLegacy);
					}
					else
					{
						html = Html.FromHtml(email.Message);
					}
					emailIntent.PutExtra(Intent.ExtraText, html);
				}
				else
				{
					emailIntent.PutExtra(Intent.ExtraText, email.Message);
				}

				if (email.Attachments.Count > 0)
				{
					var targetSdk = ResolvePackageTargetSdkVersion();

					var attachments = email.Attachments.Cast<EmailAttachment>().ToList();

					var uris = new List<IParcelable>();
					foreach (var attachment in attachments)
						if (attachment.File == null)
						{
							if (targetSdk < 24)
							{
								var uri = Uri.Parse("file://" + attachment.FilePath);
								uris.Add(uri);
							}
							else
							{
								var uri = FileProvider.GetUriForFile(Application.Context,
									Application.Context.PackageName + ".fileprovider",
									new File(attachment.FilePath));
								uris.Add(uri);
							}
						}
						else
						{
							if (targetSdk < 24)
							{
								var uri = Uri.FromFile(attachment.File);
								uris.Add(uri);
							}
							else
							{
								var uri = FileProvider.GetUriForFile(Application.Context,
									Application.Context.PackageName + ".fileprovider",
									attachment.File);
								uris.Add(uri);
							}
						}

					if (uris.Count > 1)
						emailIntent.PutParcelableArrayListExtra(Intent.ExtraStream, uris);
					else
						emailIntent.PutExtra(Intent.ExtraStream, uris[0]);

					emailIntent.AddFlags(ActivityFlags.GrantReadUriPermission);
				}

				emailIntent.StartNewTopMostActivity();
			}
		}

		private bool CanSend(Intent emailIntent)
		{
			var mgr = Application.Context.PackageManager;
			return emailIntent.ResolveActivity(mgr) != null;
		}

		private int ResolvePackageTargetSdkVersion()
		{
			int sdkVersion;
			try
			{
				sdkVersion = (int)Application.Context.ApplicationInfo.TargetSdkVersion;
			}
			catch (Exception)
			{
				var appInfo = Application.Context.PackageManager.GetApplicationInfo(Application.Context.PackageName, 0);
				sdkVersion = (int)appInfo.TargetSdkVersion;
			}

			return sdkVersion;
		}

		private Intent ResolveSendIntent(EmailMessage email)
		{
			Intent emailIntent;

			if (settings.UseStrictMode)
			{
				var intentAction = Intent.ActionSendto;
				emailIntent = new Intent(intentAction);
				emailIntent.SetData(Uri.Parse("mailto:"));
			}
			else
			{
				var intentAction = Intent.ActionSend;
				if (email.Attachments.Count > 1)
					intentAction = Intent.ActionSendMultiple;

				emailIntent = new Intent(intentAction);
				emailIntent.SetType("message/rfc822");
			}

			return emailIntent;
		}
	}
}
