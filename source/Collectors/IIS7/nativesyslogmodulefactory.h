#ifndef __MODULE_FACTORY_H__
#define __MODULE_FACTORY_H__

// Factory class for CNativeSyslogModule.
// This class is responsible for creating instances
// of CNativeSyslogModule for each request.
class CNativeSyslogModuleFactory : public IHttpModuleFactory
{
public:
    
    CNativeSyslogModuleFactory() {
        IAppHostAdminManager            * pMgr           = NULL;
        IAppHostElement                 * pModuleConfig  = NULL;
        IAppHostProperty*                 configProperty = NULL;

        BSTR    bstrSectionName         = SysAllocString(L"system.webServer/nativeSyslogPublisher");
        BSTR    bstrConfigPath          = SysAllocString(L"MACHINE/WEBROOT/APPHOST");

        BSTR    bstrServerPropName      = SysAllocString(L"logServer");
        BSTR    bstrPortPropName        = SysAllocString(L"port");
        BSTR    bstrMessageSizePropName = SysAllocString(L"maxMessageLength");
        BSTR    bstrFacilityPropName    = SysAllocString(L"facility");

        HRESULT hr = CoInitializeEx( NULL, COINIT_MULTITHREADED );
        if (SUCCEEDED(hr)) {
            hr = CoCreateInstance( __uuidof( AppHostAdminManager ), NULL, CLSCTX_INPROC_SERVER, __uuidof( IAppHostAdminManager ), (void**) &pMgr );
            if((SUCCEEDED(hr)) && (pMgr != NULL))
            {
                hr = pMgr->GetAdminSection(bstrSectionName, bstrConfigPath, &pModuleConfig);
                if(SUCCEEDED(hr) && (&pModuleConfig != NULL))
                {
                    VARIANT variantValue;
                    hr = pModuleConfig->GetPropertyByName(bstrServerPropName, &configProperty);
                    if (SUCCEEDED(hr) && configProperty) {
                        hr = configProperty->get_Value(&variantValue);
                        if (SUCCEEDED(hr)) bstrServerName = SysAllocString(variantValue.bstrVal);
                    }
                    VariantClear(&variantValue);
                    hr = pModuleConfig->GetPropertyByName(bstrPortPropName, &configProperty);
                    if (SUCCEEDED(hr) && configProperty) {
                        hr = configProperty->get_Value(&variantValue);
                        if (SUCCEEDED(hr)) bstrPort = SysAllocString(variantValue.bstrVal);
                    }
                    VariantClear(&variantValue);
                    hr = pModuleConfig->GetPropertyByName(bstrMessageSizePropName, &configProperty);
                    if (SUCCEEDED(hr) && configProperty) {
                        hr = configProperty->get_Value(&variantValue);
                        if (SUCCEEDED(hr)) uiMaxMessageSize = variantValue.uiVal;
                    }
                    VariantClear(&variantValue);
                    hr = pModuleConfig->GetPropertyByName(bstrFacilityPropName, &configProperty);
                    if (SUCCEEDED(hr) && configProperty) {
                        hr = configProperty->get_Value(&variantValue);
                        if (SUCCEEDED(hr)) uiFacility = variantValue.uiVal;
                    }
                }
            }
        }
        if (pMgr != NULL) pMgr->Release();
        if (pModuleConfig != NULL) pModuleConfig->Release();

        SysFreeString(bstrSectionName);
        SysFreeString(bstrConfigPath);
        SysFreeString(bstrServerPropName);
        SysFreeString(bstrPortPropName);
        SysFreeString(bstrMessageSizePropName);
        SysFreeString(bstrFacilityPropName);
    }

    ~CNativeSyslogModuleFactory()
    {
        if (!bstrServerName)
             SysFreeString(bstrServerName);
        if (!bstrPort)
             SysFreeString(bstrPort);
    }
    

    virtual HRESULT GetHttpModule(OUT CHttpModule **ppModule, IN IModuleAllocator*)
    {
        HRESULT hr = S_OK;

        CNativeSyslogModule *pModule = new CNativeSyslogModule();
        if (!pModule)
        {
            hr = HRESULT_FROM_WIN32( ERROR_NOT_ENOUGH_MEMORY );
        } 
        else
        {
            // Load in our settings to override the defaults.
            pModule->bstrServerName = SysAllocString(bstrServerName);
            pModule->uiMaxMessageSize = uiMaxMessageSize;
            pModule->uiFacility = uiFacility;
            pModule->bstrPort = SysAllocString(bstrPort);

            *ppModule = pModule;
            pModule = NULL;
        }
        return hr;
    }

    virtual void Terminate()
    {
        delete this;
    }

private:
    //  syslog server name
    BSTR    bstrServerName;
    //  syslog server port
    BSTR    bstrPort;
    //  syslog Max Message Size
    USHORT  uiMaxMessageSize;
    //  syslog facility
    USHORT  uiFacility;
};

#endif
