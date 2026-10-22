using Android.App;
using Android.Runtime;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Mobile.Services;

namespace CharacterPromptGenerator.Mobile;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		// 按鈕等操作中未預期的錯誤：記錄並提示，不讓 App 直接閃退。
		// 資料修改本身是原子的（複製 → 修改 → 驗證 → 原子寫檔），出錯時不會留下半套資料。
		AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
		{
			AppServices.Log(e.Exception);
			e.Handled = true;
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try { await Dialogs.Error(L.F("Mobile_UnexpectedError", e.Exception.Message)); }
				catch { /* 連提示都顯示不了時只留下記錄 */ }
			});
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
