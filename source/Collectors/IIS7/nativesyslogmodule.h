#ifndef __NATIVE_SYSLOG_MODULE_H__
#define __NATIVE_SYSLOG_MODULE_H__

//  The module implementation.
//  This class is responsible for implementing the 
//  module functionality for each of the server events
//  that it registers for.
class CNativeSyslogModule : public CHttpModule
{
public:

    CNativeSyslogModule() {
        bstrServerName = SysAllocString(L"127.0.0.1");
        bstrPort = SysAllocString(L"514");
        uiMaxMessageSize = 1024;
        uiFacility = 13;
        cstrMsgHdr = CString(_T(""));
    }

    ~CNativeSyslogModule() {
        if (bstrServerName != NULL)
            SysFreeString(bstrServerName);
        if (bstrPort != NULL)
            SysFreeString(bstrPort);
    }

    //  Global syslog server name
    BSTR    bstrServerName;
    //  Global syslog server port
    BSTR    bstrPort;
    //  Global syslog Max Message Size
    USHORT  uiMaxMessageSize;
    //  Global syslog Facility ID
    USHORT  uiFacility;

    // Global seconds.milliseconds of when the request started.
    long  longTimeSeconds;

    // The message generated with request data to send to syslog
    CString cstrMsgHdr;

    REQUEST_NOTIFICATION_STATUS
    OnSendResponse(
        IN IHttpContext *                       pHttpContext,
        IN ISendResponseProvider *              pProvider
    );

    REQUEST_NOTIFICATION_STATUS
    OnBeginRequest(
        IN IHttpContext *                       pHttpContext,
        IN IHttpEventProvider *                 pProvider
    );

private:
    // Sends a given pszMessage through a UDP socket on uiPort to bstrServerName
    HRESULT CNativeSyslogModule::SendSyslogMessage(
        PCSTR pszMessage
    );
};

#endif
