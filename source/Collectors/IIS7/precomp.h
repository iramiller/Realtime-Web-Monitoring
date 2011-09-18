#ifndef __PRECOMP_H__
#define __PRECOMP_H__

// Include fewer Windows headers 
#define WIN32_LEAN_AND_MEAN

//  IIS7 Server API header file
#include "httpserv.h"
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iphlpapi.h>
#include <atlstr.h>
#include <sys/timeb.h>
#include <time.h>

#pragma comment(lib, "Ws2_32.lib")

//  Project header files
#include "nativesyslogmodule.h"
#include "nativesyslogmodulefactory.h"

#endif

