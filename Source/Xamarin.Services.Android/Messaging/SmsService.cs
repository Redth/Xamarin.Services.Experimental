using Android.Content;
using Android.Net;
using Android.Telephony;

namespace Xamarin.Services.Messaging
{
	// NOTE: http://developer.xamarin.com/recipes/android/networking/sms/send_an_sms/

	public partial class SmsService
	{
		public bool CanSendSms => true;

		public bool CanSendSmsInBackground => true;

		public void SendSms(string recipient = null, string message = null)
		{
			message = message ?? string.Empty;

			if (CanSendSms)
			{
				Uri smsUri;
				if (!string.IsNullOrWhiteSpace(recipient))
					smsUri = Uri.Parse("smsto:" + recipient);
				else
					smsUri = Uri.Parse("smsto:");

				var smsIntent = new Intent(Intent.ActionSendto, smsUri);
				smsIntent.PutExtra("sms_body", message);

				smsIntent.StartNewTopMostActivity();
			}
		}

		public void SendSmsInBackground(string recipient, string message = null)
		{
			message = message ?? string.Empty;

			if (CanSendSmsInBackground)
			{
				var smsManager = SmsManager.Default;
				smsManager.SendTextMessage(recipient, null, message, null, null);
			}
		}
	}
}
