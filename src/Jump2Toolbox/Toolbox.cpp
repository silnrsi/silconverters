// Toolbox.cpp : Implementation of CToolbox

#include "stdafx.h"
#include "Toolbox.h"


// CToolbox
static UINT NEAR WM_SANTA_FE_FOCUS = RegisterWindowMessage("SantaFeFocus");
static UINT NEAR wm_RemoteSaveAll = RegisterWindowMessage( "ShbxSaveAll" );
static UINT NEAR wm_RemoteRefreshAll = RegisterWindowMessage( "ShbxRefreshAll" );

HRESULT InternalJump(LPCSTR lpszData)
{
    // Toolbox being a non-unicode (or non-wide anyway) app needs to see this as 8-bit UTF8 
	//  chars
	char buf[10000];
	strcpy(buf,lpszData);
	strcat(buf,"\n");
	strcat(buf,"surface");

	long lReturn = ::RegSetValueA(HKEY_CURRENT_USER, 
								"Software\\SantaFe\\Focus\\Word", 
								REG_SZ,
								buf, 
								(DWORD)strlen(buf));

	if ( lReturn == ERROR_SUCCESS )
	{
		int iType = 4; // word focus, from Nathan's VB focus code
	
		PostMessage(HWND_BROADCAST, WM_SANTA_FE_FOCUS,
			iType, 0);
	}

	return lReturn;
}

// the standard version doesn't work correctly (it needs a bigger buffer--use 4x)
#ifdef	W2A
#undef	W2A
#endif
#define W2A(lpw) (\
	((_lpw = lpw) == NULL) ? NULL : (\
		_convert = (lstrlenW(_lpw)+1)*4,\
		ATLW2AHELPER((LPSTR) alloca(_convert), _lpw, _convert, _acp)))

STDMETHODIMP CToolbox::Jump(BSTR sTarget)
{
	USES_CONVERSION;	_acp = CP_UTF8;
	return InternalJump(OLE2CA(sTarget));
}

STDMETHODIMP CToolbox::JumpLegacy(BSTR sTarget, long cp)
{
	USES_CONVERSION;	_acp = cp;
	return InternalJump(OLE2CA(sTarget));
}

STDMETHODIMP CToolbox::Reload()
{
    PostMessage(HWND_BROADCAST, wm_RemoteRefreshAll, NULL, NULL);	// don't wait for it to finish that
    return 0;
}

STDMETHODIMP CToolbox::SaveAll()
{
	// SendMessage(HWND_BROADCAST, wm_RemoteSaveAll, NULL, NULL);
    DWORD dontcare;
    SendMessageTimeout(HWND_BROADCAST, wm_RemoteSaveAll, NULL, NULL, SMTO_ABORTIFHUNG, 3000, &dontcare);
    return 0;
}

