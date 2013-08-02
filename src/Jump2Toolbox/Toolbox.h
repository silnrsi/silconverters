// Toolbox.h : Declaration of the CToolbox

#pragma once
#include "resource.h"       // main symbols


// IToolbox
[
	object,
	uuid("ABF112BA-2EB8-4517-A2AF-4DAEC4EB5C1F"),
	dual,	helpstring("IToolbox Interface"),
	pointer_default(unique)
]
__interface IToolbox : IDispatch
{
	[id(1), helpstring("Jump to a Unicode-encoded target word")] HRESULT Jump([in] BSTR sTarget);
	[id(2), helpstring("Jump to a Legacy-encoded target word")] HRESULT JumpLegacy([in] BSTR sTarget, [in,defaultvalue(0)] LONG cp);
	[id(3), helpstring("Tell Toolbox to save all files")] HRESULT SaveAll();
	[id(4), helpstring("Tell Toolbox to reload all files")] HRESULT Reload();
};



// CToolbox

[
	coclass,
	threading(apartment),
	vi_progid("Toolbox.Jump"),
	progid("Toolbox.Jump.2"),
	version(1.0),
	uuid("0429E35F-45C6-4C5D-9B02-E32EF5DE36C1"),
	helpstring("Toolbox Class")
]
class ATL_NO_VTABLE CToolbox : 
	public IToolbox
{
public:
	CToolbox()
	{
	}


	DECLARE_PROTECT_FINAL_CONSTRUCT();

	HRESULT FinalConstruct()
	{
		return S_OK;
	}
	
	void FinalRelease() 
	{
	}

public:

	STDMETHOD(Jump)(BSTR sTarget);
	STDMETHOD(JumpLegacy)(BSTR sTarget, long cp);
	STDMETHOD(SaveAll)();
	STDMETHOD(Reload)();
};

