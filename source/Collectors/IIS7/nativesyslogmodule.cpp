#include "precomp.h"
#include <sys/timeb.h>

REQUEST_NOTIFICATION_STATUS
CNativeSyslogModule::OnBeginRequest(
    IN IHttpContext * pHttpContext,
    IN IHttpEventProvider * pProvider
)
{
    UNREFERENCED_PARAMETER( pProvider );
    
    LARGE_INTEGER li;
    QueryPerformanceCounter(&li);

    struct _timeb timebuffer;
    char timeline[26];
    _ftime64_s( &timebuffer );
    ctime_s(timeline, 26, &(timebuffer.time));

    if ((uiFacility < 0) || (uiFacility > 23))
        uiFacility = 13; // fix invalid values at 13.

    USHORT uSeverity = (uiFacility << 3) + 6; // priority of 6 indicates this is an informational message
    
    USHORT *headerLength = 0;
    cstrMsgHdr.Empty();
    cstrMsgHdr.AppendFormat(_T("<%d>"), uSeverity);
    cstrMsgHdr.AppendFormat(_T("%.15s "), &timeline[4]);  // Jun 03 12:00:00

    IHttpRequest *pHttpRequest = pHttpContext->GetRequest();
    cstrMsgHdr.AppendFormat(_T("%s IISNSL: "), pHttpRequest->GetHeader(HttpHeaderHost, headerLength));     // www.sitename.com
    cstrMsgHdr.AppendFormat(_T("%8d "), (li.QuadPart/100));                                                // 123456789


    longTimeSeconds = ((long)timebuffer.time) * 1000 + timebuffer.millitm; // this coverts the seconds into milliseconds

    return RQ_NOTIFICATION_CONTINUE;
}

REQUEST_NOTIFICATION_STATUS
CNativeSyslogModule::OnSendResponse(
    IN IHttpContext *                       pHttpContext,
    IN ISendResponseProvider *              pProvider
)
{
    HRESULT hr = S_OK;
    
    // Retrieve a a pointer to the current response.
    IHttpResponse *pHttpResponse = pHttpContext->GetResponse();
    IHttpRequest *pHttpRequest = pHttpContext->GetRequest();
    IHttpApplication *pHttpApplication = pHttpContext->GetApplication();

    // Test for errors.
    if (pHttpResponse != NULL)
    {
        struct _timeb timebuffer;
        char timeline[26];
        _ftime64_s( &timebuffer );
        ctime_s(timeline, 26, &(timebuffer.time));
        long longElapsedSeconds = (((long)timebuffer.time) * 1000 + timebuffer.millitm) - longTimeSeconds; // compute out the difference between the start time and now for ttfb

        USHORT *headerLength = 0;
        USHORT uStatusCode = 0;
        USHORT uSubStatus = 0;
        // Retrieve the current HTTP status code.
        pHttpResponse->GetStatus(&uStatusCode,&uSubStatus);

        CString cstrMsgHdr2(_T(""));
        cstrMsgHdr2.AppendFormat(_T("%d.%d %d "), uStatusCode, uSubStatus, longElapsedSeconds);         // 200.0 10

        CString cstrMsgBody(pHttpRequest->GetHttpMethod());                                                             // GET
        cstrMsgBody.AppendFormat(_T(" %s "), pHttpRequest->GetRawHttpRequest()->pRawUrl);                               // /path/to/page.html

        if (pHttpResponse->GetHeader(HttpHeaderContentType, headerLength) == NULL)
            cstrMsgBody.Append(_T("- "));
        else {
            CString cstrContentType(pHttpResponse->GetHeader(HttpHeaderContentType, headerLength));
            if (cstrContentType.Find(';',0) > 0)
                cstrMsgBody.AppendFormat(_T("%s "), cstrContentType.Left(cstrContentType.Find(';',0)));
            else
                cstrMsgBody.AppendFormat(_T("%s "), cstrContentType);
        }

        if (pHttpRequest->GetRemoteAddress()!= NULL)
            cstrMsgBody.AppendFormat(_T("%s "), inet_ntoa(((PSOCKADDR_IN)pHttpRequest->GetRemoteAddress())->sin_addr)); // 10.0.0.1
        else
            cstrMsgHdr.Append(_T("- "));
        cstrMsgBody.AppendFormat(_T("%s "), pHttpApplication->GetApplicationId());                                      // /LM/W3SVC/1/ROOT
        cstrMsgBody.AppendFormat(_T("%s "), pHttpRequest->GetHeader(HttpHeaderReferer, headerLength));                  // scheme://server/path/to/the%20source.html

        CStringA strUserAgent(pHttpRequest->GetHeader(HttpHeaderUserAgent, headerLength));
        strUserAgent.Replace(_T(" "), _T("+")); // replace spaces with + signs
        cstrMsgBody.AppendFormat(_T("%s "), strUserAgent);                                                          // Mozilla/5.0+(Windows+NT+6.1;+WOW64;)

        int iMaxMessageLength = (uiMaxMessageSize - cstrMsgHdr.GetLength()) - cstrMsgHdr2.GetLength(); // length of each piece of the message body is the max length minus the headers
        int iMessageLength = cstrMsgBody.GetLength(); // total amount to send
        USHORT i = 1;
        do {
            CString cstrMsgPart(cstrMsgHdr);
            cstrMsgPart.Append(cstrMsgHdr2);
            cstrMsgPart.AppendFormat(_T("%d %d "), i, (iMessageLength / iMaxMessageLength) + 1);       // 1 2 (current message part and total message parts)

            if (iMessageLength > (i * iMaxMessageLength))
                cstrMsgPart.AppendFormat(_T("%s"), cstrMsgBody.Mid(iMaxMessageLength * (i - 1), iMaxMessageLength)); // we can't send it all so send the most we can
            else    
                cstrMsgPart.AppendFormat(_T("%s"), cstrMsgBody.Mid(iMaxMessageLength * (i - 1))); // send whatever is left since it fits in a single message

            hr = SendSyslogMessage(cstrMsgPart.GetString());
            if (FAILED(hr)) break;
        } while ((i < 4) && (i++ * iMaxMessageLength) <= iMessageLength); // the reasonable maximum number of chars in a url is about 2000. given a min mtu of 1500*4 = 6000 we have more than enough space
    }

    if ( FAILED( hr )  )
        return RQ_NOTIFICATION_FINISH_REQUEST;
    else
        return RQ_NOTIFICATION_CONTINUE;
}

// Sends the given string as a syslog message.
HRESULT CNativeSyslogModule::SendSyslogMessage(
    PCSTR pszMessage
    ) 
{
    WSADATA wsaData;
    HRESULT hr = S_OK;

    // Initialize Winsock
    hr = WSAStartup(MAKEWORD(2,2), &wsaData);
    if (SUCCEEDED(hr)) {
        ADDRINFOW 
            *result = NULL,
            *ptr = NULL,
            hints;

        ZeroMemory( &hints, sizeof(hints) );
        hints.ai_family = AF_INET;
        hints.ai_socktype = SOCK_DGRAM;
        hints.ai_protocol = IPPROTO_UDP;

        // Resolve the server address and port
        hr = GetAddrInfoW(bstrServerName, bstrPort, &hints, &result);
        
        // If we have an address
        if (SUCCEEDED(hr)) {
            SOCKET ConnectSocket = INVALID_SOCKET;
            // Attempt to connect to the first address returned by
            // the call to getaddrinfo
            ptr=result;
            // Create a SOCKET for connecting to server
            ConnectSocket = socket(ptr->ai_family, ptr->ai_socktype, ptr->ai_protocol);
            if (ConnectSocket != INVALID_SOCKET) {
                // Connect to server.
                hr = connect(ConnectSocket, ptr->ai_addr, (int)ptr->ai_addrlen);
                
                if (hr != SOCKET_ERROR) {
                    // compute the socket reported max message size... if it is smaller then that is what we will use.
                    int intMaxMsgSize;
                    int argSize = sizeof(int);
                    hr = getsockopt(ConnectSocket, SOL_SOCKET, SO_MAX_MSG_SIZE, (char*)&intMaxMsgSize, &argSize);
                    if (intMaxMsgSize < uiMaxMessageSize)
                        intMaxMsgSize = uiMaxMessageSize;

                    size_t uLength = strlen(pszMessage);
                    if (uLength > intMaxMsgSize)
                        uLength = intMaxMsgSize;
                    // Send an initial buffer
                    hr = send(ConnectSocket, pszMessage, uLength, 0);
                    // TODO: check for failed send... abort
                    hr = shutdown(ConnectSocket, SD_SEND);
                }
                closesocket(ConnectSocket);
                ConnectSocket = INVALID_SOCKET;
            }
            
            else // failure to open socket
            { 
                int errNum = WSAGetLastError();
                // trace out error message here.
            }
        }
        // clean up any found address records
        FreeAddrInfoW(result);
    }
    // take down the winsock
    WSACleanup();
    return hr;
}