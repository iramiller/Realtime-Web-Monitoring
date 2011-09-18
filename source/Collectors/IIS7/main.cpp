#include "precomp.h"

HRESULT
__stdcall
RegisterModule(
    DWORD dwServerVersion,
    IHttpModuleRegistrationInfo *pModuleInfo,
    IHttpServer *pHttpServer
)
{
    HRESULT  hr = pModuleInfo->SetRequestNotifications(new CNativeSyslogModuleFactory(), RQ_BEGIN_REQUEST | RQ_SEND_RESPONSE, 0);
    if (SUCCEEDED(hr))
    {
        // to properly capture time we need to capture the starting ticks before any others.
        hr = pModuleInfo->SetPriorityForRequestNotification(RQ_BEGIN_REQUEST,PRIORITY_ALIAS_FIRST);
        if (SUCCEEDED(hr))
        {
            // For send response priority is inverted.  Because we are capturing timing for TTFB we come first (lowest priority) here.
            hr = pModuleInfo->SetPriorityForRequestNotification(RQ_SEND_RESPONSE,PRIORITY_ALIAS_LAST);
        }
    }
    return hr;
}
