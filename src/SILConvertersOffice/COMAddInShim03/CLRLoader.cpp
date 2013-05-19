#include "StdAfx.h"
#include "clrloader.h"
#include "metahost.h"  // CLR 40 hosting interfaces

// When loading assemblies targeting CLR 4.0 and above make sure
// the below line is NOT commented out. It enables starting
// CLR using new hosting interfaces available in CLR 4.0 and above.
// When below line is commeted out the shim will use legacy interfaces 
// which only allow loading CLR 2.0 or below.
#define USE_CLR40_HOSTING

#ifdef USE_CLR40_HOSTING
static LPCWSTR g_wszAssemblyFileName =
    L"SILConvertersOffice.dll";
#endif

using namespace mscorlib;

static HRESULT GetDllDirectory(TCHAR *szPath, DWORD nPathBufferSize);


CCLRLoader::CCLRLoader(void)
    : m_pCorRuntimeHost(NULL), m_pAppDomain(NULL)
{
}

// CreateInstance: loads the CLR, creates an AppDomain, and creates an
// aggregated instance of the target managed add-in in that AppDomain.
HRESULT CCLRLoader::CreateAggregatedAddIn(
    IUnknown* pOuter,
    LPCWSTR szAssemblyName,
    LPCWSTR szClassName,
    LPCWSTR szAssemblyConfigName)
{
    HRESULT hr = E_FAIL;

    CComPtr<_ObjectHandle>                              srpObjectHandle;
    CComPtr<ManagedHelpers::IManagedAggregator >        srpManagedAggregator;
    CComPtr<IComAggregator>                             srpComAggregator;
    CComVariant                                         cvarManagedAggregator;

    // Load the CLR, and create an AppDomain for the target assembly.
    IfFailGo( LoadCLR() );
    IfFailGo( CreateAppDomain(szAssemblyConfigName) );

    // Create the managed aggregator in the target AppDomain, and unwrap it.
    // This component needs to be in a location where fusion will find it, ie
    // either in the GAC or in the same folder as the shim and the add-in.
    IfFailGo( m_pAppDomain->CreateInstance(
        CComBSTR(L"SecOffice_ManagedAggregator03, PublicKeyToken=d51fbf4dbc2f7f14"),
        CComBSTR(L"ManagedHelpers.ManagedAggregator"),
        &srpObjectHandle) );
    IfFailGo( srpObjectHandle->Unwrap(&cvarManagedAggregator) );
    IfFailGo( cvarManagedAggregator.pdispVal->QueryInterface(
        &srpManagedAggregator) );

    // Instantiate and aggregate the inner managed add-in into the outer
    // (unmanaged, ConnectProxy) object.
    IfFailGo( pOuter->QueryInterface(
        __uuidof(IComAggregator), (LPVOID*)&srpComAggregator) );
    IfFailGo( srpManagedAggregator->CreateAggregatedInstance(
        CComBSTR(szAssemblyName), CComBSTR(szClassName), srpComAggregator) );

Error:
    return hr;
}

#ifdef USE_CLR40_HOSTING

// Convert "vN.N.N" into an array of numbers
static void ParseClrVersion(LPCWSTR wszVersion, int rgiVersion[3])
{
    rgiVersion[0] = rgiVersion[1] = rgiVersion[2] = 0;

    LPCWCH pwch = wszVersion;
    for (int i = 0; i < 3; i++)
    {
        // skip the firtst character - either 'v' or '.' and add the numbers
        for (pwch++; L'0' <= *pwch && *pwch <= L'9'; pwch++) 
            rgiVersion[i] = rgiVersion[i] * 10 + *pwch - L'0';

        if (*pwch == 0)
            break;

        assert ( *pwch == L'.' && L"we should expect a period. Otherwise it is not a proper CLR version string");
        if (*pwch != L'.')
        {
            // the input is invalid - do not parse any further
            break;
        }
    }
}

// compare order of CLR versions represented as array of numbers
static BOOL IsClrVersionHigher(int rgiVersion[3], int rgiVersion2[3])
{
    for (int i = 0; i < 3; i++)
    {
        if (rgiVersion[i] != rgiVersion2[i])
            return rgiVersion[i] > rgiVersion2[i];
    }

    return FALSE;
}

static HRESULT FindLatestInstalledRuntime(ICLRMetaHost* pMetaHost, LPCWSTR wszMinVersion, ICLRRuntimeInfo** ppRuntimeInfo)
{
    CComPtr<IEnumUnknown> srpEnum;
    CComPtr<ICLRRuntimeInfo> srpRuntimeInfo, srpLatestRuntimeInfo;
    ULONG cFetched;
    WCHAR rgwchVersion[30];
    DWORD cwchVersion;
    int rgiMinVersion[3]; //Major.Minor.Build
    int rgiVersion[3]; // Major.Minor.Build
    HRESULT hr = S_OK;

    *ppRuntimeInfo = NULL;

    // convert vN.N.N into an array of numbers
    ParseClrVersion(wszMinVersion, rgiMinVersion);

    IfFailGo( pMetaHost->EnumerateInstalledRuntimes(&srpEnum) );
    while (true)
    {
        srpRuntimeInfo.Release();
        IfFailGo( srpEnum->Next(1, (IUnknown**)&srpRuntimeInfo, &cFetched) );
        if (hr == S_FALSE)
            break;

        cwchVersion = ARRAYSIZE(rgwchVersion);
        IfFailGo( srpRuntimeInfo->GetVersionString(rgwchVersion, &cwchVersion) );

        ParseClrVersion(rgwchVersion, rgiVersion);
        if (IsClrVersionHigher(rgiVersion, rgiMinVersion) == FALSE)
            continue;

        rgiMinVersion[0] = rgiVersion[0];
        rgiMinVersion[1] = rgiVersion[1];
        rgiMinVersion[2] = rgiVersion[2];

        srpLatestRuntimeInfo.Attach(srpRuntimeInfo.Detach());
    }

    if (srpLatestRuntimeInfo == NULL)
    {
        hr = E_FAIL;
        goto Error;
    }

    hr = S_OK;
    *ppRuntimeInfo = srpLatestRuntimeInfo.Detach();

Error:
    return hr;
}

static HRESULT BindToCLR4OrAbove(ICorRuntimeHost** ppCorRuntimeHost)
{
    HRESULT hr;
    CComPtr<ICLRMetaHost> srpMetaHost;
    CComPtr<ICLRRuntimeInfo> srpRuntimeInfo;
    WCHAR rgwchPath[MAX_PATH + 1];
    WCHAR rgwchVersion[30];
    DWORD cwchVersion = ARRAYSIZE(rgwchVersion);

    *ppCorRuntimeHost = NULL;

    IfFailGo( CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (void**)&srpMetaHost) );

    // Get the location of the hosting shim DLL, and retrieve the required
    // CLR runtime version from its metadata
    IfFailGo( GetDllDirectory(rgwchPath, ARRAYSIZE(rgwchPath)) );
    if (!PathAppend(rgwchPath, g_wszAssemblyFileName))
    {
        hr = E_UNEXPECTED;
        goto Error;
    }
    IfFailGo( srpMetaHost->GetVersionFromFile(rgwchPath, rgwchVersion, &cwchVersion) );

    // First try binding to the same version of CLR the add-in is built against
    hr = srpMetaHost->GetRuntime(rgwchVersion, IID_ICLRRuntimeInfo, (void**)&srpRuntimeInfo);
    if (FAILED(hr))
    {
        // If we're here - it means the exact same version of the CLR we built against is not available.
        // In this case we will just load the highest compatible version
        srpRuntimeInfo.Release();
        IfFailGo( FindLatestInstalledRuntime(srpMetaHost, rgwchVersion, &srpRuntimeInfo) );
    }

    // we ignore the result of SetDefaultStartupFlags - this is not critical operation
    srpRuntimeInfo->SetDefaultStartupFlags(STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN_HOST, NULL);

    IfFailGo( srpRuntimeInfo->GetInterface(CLSID_CorRuntimeHost, IID_ICorRuntimeHost, (void**)ppCorRuntimeHost) );

Error:
    return hr;
}

#endif

// LoadCLR: loads and starts the .NET CLR.
HRESULT CCLRLoader::LoadCLR()
{
    HRESULT hr = S_OK;

    // Ensure the CLR is only loaded once.
    if (m_pCorRuntimeHost != NULL)
    {
        return hr;
    }

#ifdef USE_CLR40_HOSTING
    hr = BindToCLR4OrAbove(&m_pCorRuntimeHost);
#else

#pragma warning( push )
#pragma warning( disable : 4996 )
    // Load the CLR into the process, using the default (latest) version,
    // the default ("wks") flavor, and default (single) domain.
    hr = CorBindToRuntimeEx(
        NULL, NULL, STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN_HOST,
        CLSID_CorRuntimeHost, IID_ICorRuntimeHost,
        (LPVOID*)&m_pCorRuntimeHost);
#pragma warning( pop )

#endif

    // If CorBindToRuntimeEx returned a failure HRESULT, we failed to
    // load the CLR.
    if (FAILED(hr))
    {
        return hr;
    }

    // Start the CLR.
    return m_pCorRuntimeHost->Start();
}

// In order to securely load an assembly, its fully qualified strong name
// and not the filename must be used. To do that, the target AppDomain's
// base directory needs to point to the directory where the assembly is.
HRESULT CCLRLoader::CreateAppDomain(LPCWSTR szAssemblyConfigName)
{
    USES_CONVERSION;
    HRESULT hr = S_OK;

    // Ensure the AppDomain is created only once.
    if (m_pAppDomain != NULL)
    {
        return hr;
    }

    CComPtr<IUnknown> pUnkDomainSetup;
    CComPtr<IAppDomainSetup> pDomainSetup;
    CComPtr<IUnknown> pUnkAppDomain;
    TCHAR szDirectory[MAX_PATH + 1];
    TCHAR szAssemblyConfigPath[MAX_PATH + 1];
    CComBSTR cbstrAssemblyConfigPath;

    // Create an AppDomainSetup with the base directory pointing to the
    // location of the managed DLL. We assume that the target assembly
    // is located in the same directory.
    IfFailGo( m_pCorRuntimeHost->CreateDomainSetup(&pUnkDomainSetup) );
    IfFailGo( pUnkDomainSetup->QueryInterface(
        __uuidof(pDomainSetup), (LPVOID*)&pDomainSetup) );

    // Get the location of the hosting shim DLL, and configure the
    // AppDomain to search for assemblies in this location.
    IfFailGo( GetDllDirectory(
        szDirectory, sizeof(szDirectory)/sizeof(szDirectory[0])) );
    pDomainSetup->put_ApplicationBase(CComBSTR(szDirectory));

    // Set the AppDomain to use a local DLL config if there is one.
    IfFailGo( StringCchCopy(
        szAssemblyConfigPath,
        sizeof(szAssemblyConfigPath)/sizeof(szAssemblyConfigPath[0]),
        szDirectory) );
    if (!PathAppend(szAssemblyConfigPath, szAssemblyConfigName))
    {
        hr = E_UNEXPECTED;
        goto Error;
    }
    IfFailGo( cbstrAssemblyConfigPath.Append(szAssemblyConfigPath) );
    IfFailGo( pDomainSetup->put_ConfigurationFile(cbstrAssemblyConfigPath) );

    // Create an AppDomain that will run the managed assembly, and get the
    // AppDomain's _AppDomain pointer from its IUnknown pointer.
    IfFailGo( m_pCorRuntimeHost->CreateDomainEx(T2W(szDirectory),
        pUnkDomainSetup, 0, &pUnkAppDomain) );
    IfFailGo( pUnkAppDomain->QueryInterface(
        __uuidof(m_pAppDomain), (LPVOID*)&m_pAppDomain) );

Error:
   return hr;
}

// GetDllDirectory: gets the directory location of the DLL containing this
// code - that is, the shim DLL. The target add-in DLL will also be in this
// directory.
static HRESULT GetDllDirectory(TCHAR *szPath, DWORD nPathBufferSize)
{
    // Get the shim DLL module instance, or bail.
    HMODULE hInstance = _AtlBaseModule.GetModuleInstance();
    if (hInstance == 0)
    {
        return E_FAIL;
    }

    // Get the shim DLL filename, or bail.
    TCHAR szModule[MAX_PATH + 1];
    DWORD dwFLen = ::GetModuleFileName(hInstance, szModule, MAX_PATH);
    if (dwFLen == 0)
    {
        return E_FAIL;
    }

    // Get the full path to the shim DLL, or bail.
    TCHAR *pszFileName;
    dwFLen = ::GetFullPathName(
        szModule, nPathBufferSize, szPath, &pszFileName);
    if (dwFLen == 0 || dwFLen >= nPathBufferSize)
    {
        return E_FAIL;
    }

    *pszFileName = 0;
    return S_OK;
}

// Unload the AppDomain. This will be called by the ConnectProxy
// in OnDisconnection.
HRESULT CCLRLoader::Unload(void)
{
    HRESULT hr = S_OK;
    IUnknown* pUnkDomain = NULL;
    IfFailGo(m_pAppDomain->QueryInterface(
        __uuidof(IUnknown), (LPVOID*)&pUnkDomain));
    hr = m_pCorRuntimeHost->UnloadDomain(pUnkDomain);

    // Added in 2.0.2.0, only for Add-ins.
    m_pAppDomain->Release();
    m_pAppDomain = NULL;

Error:
    if (pUnkDomain != NULL)
    {
        pUnkDomain->Release();
    }
    return hr;
}

CCLRLoader::~CCLRLoader(void)
{
    if (m_pAppDomain)
    {
        m_pAppDomain->Release();
    }
    if (m_pCorRuntimeHost)
    {
        m_pCorRuntimeHost->Release();
    }
}
