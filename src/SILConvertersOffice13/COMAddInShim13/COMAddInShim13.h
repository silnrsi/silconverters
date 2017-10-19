

/* this ALWAYS GENERATED file contains the definitions for the interfaces */


 /* File created by MIDL compiler version 8.00.0603 */
/* at Sat Apr 22 16:42:38 2017
 */
/* Compiler settings for COMAddInShim13.idl:
    Oicf, W1, Zp8, env=Win32 (32b run), target_arch=X86 8.00.0603 
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
#endif // __RPCNDR_H_VERSION__


#ifndef __COMAddInShim13_h__
#define __COMAddInShim13_h__

#if defined(_MSC_VER) && (_MSC_VER >= 1020)
#pragma once
#endif

/* Forward Declarations */ 

#ifndef __ConnectProxy_FWD_DEFINED__
#define __ConnectProxy_FWD_DEFINED__

#ifdef __cplusplus
typedef class ConnectProxy ConnectProxy;
#else
typedef struct ConnectProxy ConnectProxy;
#endif /* __cplusplus */

#endif 	/* __ConnectProxy_FWD_DEFINED__ */


/* header files for imported files */
#include "oaidl.h"
#include "ocidl.h"

#ifdef __cplusplus
extern "C"{
#endif 



#ifndef __COMAddInShim13Lib_LIBRARY_DEFINED__
#define __COMAddInShim13Lib_LIBRARY_DEFINED__

/* library COMAddInShim13Lib */
/* [helpstring][version][uuid] */ 


EXTERN_C const IID LIBID_COMAddInShim13Lib;

EXTERN_C const CLSID CLSID_ConnectProxy;

#ifdef __cplusplus

class DECLSPEC_UUID("9fdb0283-1795-4bf4-97e9-84223f3fc8ea")
ConnectProxy;
#endif
#endif /* __COMAddInShim13Lib_LIBRARY_DEFINED__ */

/* Additional Prototypes for ALL interfaces */

/* end of Additional Prototypes */

#ifdef __cplusplus
}
#endif

#endif


