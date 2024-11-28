using Comfort.Common;
using EFT;
using EFT.UI;
using SPT.Reflection.Patching;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Fika.Core.UI.Patches
{
	public class TOS_Patch : ModulePatch
	{
		protected const string str_1 = "V2VsY29tZSB0byBNVEMtQyEKCk1UQy1DIHVzZXMgRmlrYSBhcyBhIGJhc2UgZm9yIG91ciBtdWx0aXBsYXllci4gRmlrYSBpcyBhIGNvLW9wIG1vZCBmb3IgU1BUIHRoYXQgbGV0cyB5b3UgcGxheSBjb29wIEVGVCB3aXRoIHlvdXIgZnJpZW5kcy4gRmlrYSBpcyBmcmVlLCBpZiB5b3UgcGFpZCB0byBqb2luIHRoaXMgc2VydmVyIG9yIGFueSBvdGhlciBzZXJ2ZXIsIHlvdSBnb3Qgc2NhbW1lZC4gWW91IGFyZSBub3QgYWxsb3dlZCB0byBob3N0IHB1YmxpYyBzZXJ2ZXJzIHdpdGggbW9uZXRpemF0aW9uIG9yIGRvbmF0aW9ucy4gQW55IHNlcnZlcnMgdGhhdCBoYXZlIHBheW1lbnQgb2YgYW55IGtpbmQgd2lsbCBiZSBzaHV0IGRvd24uCgpXYWl0IGZvciB0aGlzIG1lc3NhZ2UgdG8gZmFkZSB0byBhY2NlcHQgdGhlIEZpa2EgVGVybXMgb2YgU2VydmljZS4KCllvdSBjYW4gam9pbiB0aGUgRmlrYSBEaXNjb3JkIGhlcmU6IGh0dHBzOi8vZGlzY29yZC5nZy9wcm9qZWN0LWZpa2EKClRoYW5rIHlvdSB0byB0aGUgRmlrYSBkZXYgdGVhbSBmb3IgbWFraW5nIHN1Y2ggYSB3b25kZXJmdWwgcGx1Z2luIDwz";
		protected const string str_2 = "V2VsY29tZSB0byBNVEMtQyEKCk1UQy1DIHVzZXMgRmlrYSBhcyBhIGJhc2UgZm9yIG91ciBtdWx0aXBsYXllci4gWW91IGFyZSBub3QgYWxsb3dlZCB0byBob3N0IHB1YmxpYyBzZXJ2ZXJzIHdpdGggbW9uZXRpemF0aW9uIG9yIGRvbmF0aW9ucy4gQW55IHNlcnZlcnMgdGhhdCBoYXZlIHBheW1lbnQgb2YgYW55IGtpbmQgd2lsbCBiZSBzaHV0IGRvd24uCgpZb3UgY2FuIGpvaW4gdGhlIEZpa2EgRGlzY29yZCBoZXJlOiBodHRwczovL2Rpc2NvcmQuZ2cvcHJvamVjdC1maWthCgpUaGFuayB5b3UgdG8gdGhlIEZpa2EgZGV2IHRlYW0gZm9yIG1ha2luZyBzdWNoIGEgd29uZGVyZnVsIHBsdWdpbiA8Mw==";

		private static bool HasShown = false;

		protected override MethodBase GetTargetMethod() => typeof(TarkovApplication).GetMethod(nameof(TarkovApplication.method_23));

		[PatchPostfix]
		public static void Postfix()
		{
			if (HasShown)
			{
				return;
			}

			HasShown = true;

			if (!FikaPlugin.AcceptedTOS.Value)
			{
				byte[] str_1_b = Convert.FromBase64String(str_1);
				string str_1_d = Encoding.UTF8.GetString(str_1_b);
				Singleton<PreloaderUI>.Instance.ShowFikaMessage("FIKA", str_1_d, ErrorScreen.EButtonType.QuitButton, 15f,
					Application.Quit, AcceptTos);
			}
			else
			{
				byte[] str_2_b = Convert.FromBase64String(str_2);
				string str_2_d = Encoding.UTF8.GetString(str_2_b);
				Singleton<PreloaderUI>.Instance.ShowFikaMessage("FIKA", str_2_d, ErrorScreen.EButtonType.OkButton, 0f,
					null,
					null);
			}
		}

		private static void AcceptTos()
		{
			FikaPlugin.AcceptedTOS.Value = true;
		}
	}
}
