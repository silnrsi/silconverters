// COMAddInShim13.cpp : Implementation of DLL Exports.

#include "stdafx.h"
#include "resource.h"

class CCOMAddInShim13Module : 
	public CAtlDllModuleT< CCOMAddInShim13Module >
{
public :
	DECLARE_NO_REGISTRY()
};

CCOMAddInShim13Module _AtlModule;

// DLL Entry Point.
extern "C" BOOL WINAPI DllMain(
	HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
    return _AtlModule.DllMain(dwReason, lpReserved); 
}

// Used to determine whether the DLL can be unloaded by OLE.
STDAPI DllCanUnloadNow(void)
{
    return _AtlModule.DllCanUnloadNow();
}

// Returns a class factory to create an object of the requested type.
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    return _AtlModule.DllGetClassObject(rclsid, riid, ppv);
}

// DllRegisterServer - Adds entries to the system registry.
STDAPI DllRegisterServer(void)
{
    // Registers object, typelib and all interfaces in typelib.
    return _AtlModule.DllRegisterServer(FALSE);
}

// DllUnregisterServer - Removes entries from the system registry.
STDAPI DllUnregisterServer(void)
{
	return _AtlModule.DllUnregisterServer(FALSE);
}
