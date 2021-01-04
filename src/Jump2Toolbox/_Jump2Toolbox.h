

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.01.0622 */
/* at Mon Jan 18 21:14:07 2038
 */
/* Compiler settings for _Jump2Toolbox.idl:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.01.0622 
    protocol : dce , ms_ext, c_ext, robust
    error checks: allocation ref bounds_check enum stub_data 
    VC __declspec() decoration level: 
         __declspec(uuid()), __declspec(selectany), __declspec(novtable)
         DECLSPEC_UUID(), MIDL_INTERFACE()
*/
/* @@MIDL_FILE_HEADING(  ) */

#pragma warning( disable: 4049 )  /* more than 64k source lines */


/* verify that the <rpcndr.h> version is high enough to compile this file*/
#ifndef __REQUIRED_RPCNDR_H_VERSION__
#define __REQUIRED_RPCNDR_H_VERSION__ 475
#endif

#include "rpc.h"
#include "rpcndr.h"

#ifndef __RPCNDR_H_VERSION__
#error this stub requires an updated version of <rpcndr.h>
#endif /* __RPCNDR_H_VERSION__ */

#ifndef COM_NO_WINDOWS_H
#include "windows.h"
#include "ole2.h"
#endif /*COM_NO_WINDOWS_H*/

#ifndef ___Jump2Toolbox_h__
#define ___Jump2Toolbox_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __IToolbox_FWD_DEFINED__
#define __IToolbox_FWD_DEFINED__
typedef interface IToolbox IToolbox;

#endif 	/* __IToolbox_FWD_DEFINED__ */


#ifndef __CToolbox_FWD_DEFINED__
#define __CToolbox_FWD_DEFINED__

#ifdef __cplusplus
typedef class CToolbox CToolbox;
#else
typedef struct CToolbox CToolbox;
#endif /* __cplusplus */

#endif 	/* __CToolbox_FWD_DEFINED__ */


/* header files for imported files */
#include "prsht.h"
#include "MsHTML.h"
#include "MsHtmHst.h"
#include "ExDisp.h"
#include "ObjSafe.h"

#ifdef __cplusplus
extern "C"{
#endif 


#ifndef __IToolbox_INTERFACE_DEFINED__
#define __IToolbox_INTERFACE_DEFINED__

/* interface IToolbox */
/* [unique][helpstring][dual][uuid][object] */ 


EXTERN_C const IID IID_IToolbox;

#if defined(__cplusplus) && !defined(CINTERFACE)
    
    MIDL_INTERFACE("ABF112BA-2EB8-4517-A2AF-4DAEC4EB5C1F")
    IToolbox : public IDispatch
    {
    public:
        virtual /* [helpstring][id] */ HRESULT STDMETHODCALLTYPE Jump( 
            /* [in] */ BSTR sTarget) = 0;
        
        virtual /* [helpstring][id] */ HRESULT STDMETHODCALLTYPE JumpLegacy( 
            /* [in] */ BSTR sTarget,
            /* [defaultvalue][in] */ LONG cp = 0) = 0;
        
        virtual /* [helpstring][id] */ HRESULT STDMETHODCALLTYPE SaveAll( void) = 0;
        
        virtual /* [helpstring][id] */ HRESULT STDMETHODCALLTYPE Reload( void) = 0;
        
    };
    
    
#else 	/* C style interface */

    typedef struct IToolboxVtbl
    {
        BEGIN_INTERFACE
        
        HRESULT ( STDMETHODCALLTYPE *QueryInterface )( 
            IToolbox * This,
            /* [in] */ REFIID riid,
            /* [annotation][iid_is][out] */ 
            _COM_Outptr_  void **ppvObject);
        
        ULONG ( STDMETHODCALLTYPE *AddRef )( 
            IToolbox * This);
        
        ULONG ( STDMETHODCALLTYPE *Release )( 
            IToolbox * This);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfoCount )( 
            IToolbox * This,
            /* [out] */ UINT *pctinfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetTypeInfo )( 
            IToolbox * This,
            /* [in] */ UINT iTInfo,
            /* [in] */ LCID lcid,
            /* [out] */ ITypeInfo **ppTInfo);
        
        HRESULT ( STDMETHODCALLTYPE *GetIDsOfNames )( 
            IToolbox * This,
            /* [in] */ REFIID riid,
            /* [size_is][in] */ LPOLESTR *rgszNames,
            /* [range][in] */ UINT cNames,
            /* [in] */ LCID lcid,
            /* [size_is][out] */ DISPID *rgDispId);
        
        /* [local] */ HRESULT ( STDMETHODCALLTYPE *Invoke )( 
            IToolbox * This,
            /* [annotation][in] */ 
            _In_  DISPID dispIdMember,
            /* [annotation][in] */ 
            _In_  REFIID riid,
            /* [annotation][in] */ 
            _In_  LCID lcid,
            /* [annotation][in] */ 
            _In_  WORD wFlags,
            /* [annotation][out][in] */ 
            _In_  DISPPARAMS *pDispParams,
            /* [annotation][out] */ 
            _Out_opt_  VARIANT *pVarResult,
            /* [annotation][out] */ 
            _Out_opt_  EXCEPINFO *pExcepInfo,
            /* [annotation][out] */ 
            _Out_opt_  UINT *puArgErr);
        
        /* [helpstring][id] */ HRESULT ( STDMETHODCALLTYPE *Jump )( 
            IToolbox * This,
            /* [in] */ BSTR sTarget);
        
        /* [helpstring][id] */ HRESULT ( STDMETHODCALLTYPE *JumpLegacy )( 
            IToolbox * This,
            /* [in] */ BSTR sTarget,
            /* [defaultvalue][in] */ LONG cp);
        
        /* [helpstring][id] */ HRESULT ( STDMETHODCALLTYPE *SaveAll )( 
            IToolbox * This);
        
        /* [helpstring][id] */ HRESULT ( STDMETHODCALLTYPE *Reload )( 
            IToolbox * This);
        
        END_INTERFACE
    } IToolboxVtbl;

    interface IToolbox
    {
        CONST_VTBL struct IToolboxVtbl *lpVtbl;
    };

    

#ifdef COBJMACROS


#define IToolbox_QueryInterface(This,riid,ppvObject)	\
    ( (This)->lpVtbl -> QueryInterface(This,riid,ppvObject) ) 

#define IToolbox_AddRef(This)	\
    ( (This)->lpVtbl -> AddRef(This) ) 

#define IToolbox_Release(This)	\
    ( (This)->lpVtbl -> Release(This) ) 


#define IToolbox_GetTypeInfoCount(This,pctinfo)	\
    ( (This)->lpVtbl -> GetTypeInfoCount(This,pctinfo) ) 

#define IToolbox_GetTypeInfo(This,iTInfo,lcid,ppTInfo)	\
    ( (This)->lpVtbl -> GetTypeInfo(This,iTInfo,lcid,ppTInfo) ) 

#define IToolbox_GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId)	\
    ( (This)->lpVtbl -> GetIDsOfNames(This,riid,rgszNames,cNames,lcid,rgDispId) ) 

#define IToolbox_Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr)	\
    ( (This)->lpVtbl -> Invoke(This,dispIdMember,riid,lcid,wFlags,pDispParams,pVarResult,pExcepInfo,puArgErr) ) 


#define IToolbox_Jump(This,sTarget)	\
    ( (This)->lpVtbl -> Jump(This,sTarget) ) 

#define IToolbox_JumpLegacy(This,sTarget,cp)	\
    ( (This)->lpVtbl -> JumpLegacy(This,sTarget,cp) ) 

#define IToolbox_SaveAll(This)	\
    ( (This)->lpVtbl -> SaveAll(This) ) 

#define IToolbox_Reload(This)	\
    ( (This)->lpVtbl -> Reload(This) ) 

#endif /* COBJMACROS */


#endif 	/* C style interface */




#endif 	/* __IToolbox_INTERFACE_DEFINED__ */



#ifndef __Jump2Toolbox_LIBRARY_DEFINED__
#define __Jump2Toolbox_LIBRARY_DEFINED__

/* library Jump2Toolbox */
/* [helpstring][uuid][version] */ 


EXTERN_C const IID LIBID_Jump2Toolbox;

EXTERN_C const CLSID CLSID_CToolbox;

#ifdef __cplusplus

class DECLSPEC_UUID("0429E35F-45C6-4C5D-9B02-E32EF5DE36C1")
CToolbox;
#endif
#endif /* __Jump2Toolbox_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

unsigned long             __RPC_USER  BSTR_UserSize(     unsigned long *, unsigned long            , BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserMarshal(  unsigned long *, unsigned char *, BSTR * ); 
unsigned char * __RPC_USER  BSTR_UserUnmarshal(unsigned long *, unsigned char *, BSTR * ); 
void                      __RPC_USER  BSTR_UserFree(     unsigned long *, BSTR * ); 

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


