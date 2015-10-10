using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using Avaya.ApplicationEnablement.DMCC;

namespace DMCC_iface
{
    public struct DMCC_phone_times
    {
       public DateTime  InitTime ;
       public DateTime  DeliveryTime ;
       public DateTime  ConnectTime ;
       public DateTime  ClearTime ;
    }

    public struct DMCC_phone_events
    {
       public     bool  Failed ;
       public     bool  Failed_t ;
       public     bool  NetworkReached ;
       public     bool  Delivered ;
       public     bool  Delivered_t ;
       public     bool  Established ;
       public     bool  Established_t ;
       public     bool  Transfered ;
       public     bool  CallCleared ;
       public     bool  ConnectionCleared ;
       public     bool  MonitorStopped ;
       public     bool  MediaTonesFlushed ;
       public   string  MediaTones ;
    }

    public class DMCC_service: IDisposable 
    {
/*******************************************************************************/
/*                                                                             */
/*                            Элементы класса                                  */

    private          string  mServiceIP ;
    private             int  mServicePort ;
    private          string  mApplication ;
    private          string  mUserName ;
    private          string  mUserPassword ;

    private            bool  mConnected ;
    private          string  mError ;

    private ServiceProvider  mService ;
    private            bool  mWaitReply ;
    private             int  mWaitID ;
    private          string  mSessionID ;

    private       const int  mTimeOutQuant  =100;
    private       const int  mTimeOutDefault=30000;

/*******************************************************************************/
/*                                                                             */
/*                            Список телефонов                                 */

    public class DMCC_phone
    {
           public                                   Device  handle ;
           public                                   string  Extension ;       /* Номер телефона */
           public                                   string  Switch ;          /* Имя свича */
           public                                   string  DeviceId  ;       /* Идентификатор устройства 1-st party */
           public                                   string  DeviceId_3 ;      /* Идентификатор устройства 3-rd party */
           public  ThirdPartyCallController.CallIdentifier  ConLocalId ;      /* Идентификатор соединения, местный */
           public  ThirdPartyCallController.CallIdentifier  ConLocalId_t ;    /* Идентификатор transfer-соединения, местный */
           public                                   string  ConGlobalId ;     /* Идентификатор соединения, глобальный */
           public                                   string  ConGlobalId_t ;   /* Идентификатор transfer-соединения, глобальный */
           public                                   string  CallMonitorId ;   /* Идентификатор монитора соединения */
           public                                   string  CallMonitorId_t ; /* Идентификатор монитора transfer-соединения */
           public                                   string  CallStatus ;      /* Статус соединения */
           public                                   string  MediaMonitorId ;  /* Идентификатор Media-монитора */
           public                                     bool  Terminal ;        /* Флаг регистрации терминала */
           public                                      int  InvokeId ;        /* Референс ожидаемого запроса */
           public                                   string  LastError ;       /* Последняя ошибка */
           public                         DMCC_phone_times  Times ;           /* Временные метки событий */
           public                        DMCC_phone_events  Events ;          /* Метки событий */
           public                                      int  idx ;
           public                                      int  used ;
           public                                     bool  transfer ;        /* Флаг использования transfer-режима */

        public DMCC_phone()
        {
            handle=null;
               idx=  0;
              used=  0;
          transfer=false ;
        }
    }

    private  const int  mPHONES_MAX=50;

    private DMCC_phone[]  mPhones;

    private int  iGetFreePhone() 
    {
       int  i;

        lock(this) 
        {
           for(i=0 ; i<mPHONES_MAX ; i++)
           {
             if(mPhones[i]     ==null) {  mPhones[i]     =new DMCC_phone() ;
                                          mPhones[i].idx =1 ;
                                          mPhones[i].used=1 ;  return(i) ;  }
             if(mPhones[i].used==  0 ) {  mPhones[i].used=1 ;  return(i) ;  }
           }
        }

        return(-1) ;
    }

    private void  iReleasePhone(int  idx) 
    {
       mPhones[idx].used=0 ;
    }

/*******************************************************************************/
/*                                                                             */
/*                            Свойства класса                                  */

    public string ServiceIP    {  get {  return mServiceIP       ;  }
                                  set {         mServiceIP=value ;  }
                               }
    public    int ServicePort  {  get {  return mServicePort       ;  }
                                  set {         mServicePort=value ;  }
                               }
    public string Application  {  get {  return mApplication       ;  }
                                  set {         mApplication=value ;  }
                               }
    public string UserName     {  get {  return mUserName       ;  }
                                  set {         mUserName=value ;  }
                               }
    public string UserPassword {  get {  return mUserPassword       ;  }
                                  set {         mUserPassword=value ;  }
                               }
    public string Error        {  get {  return mError              ;  }
                               }
    public   bool Connected    {  get {  return mConnected          ;  }
                               }

/*******************************************************************************/
/*                                                                             */
/*                            Конструктор                                      */

  public DMCC_service()
{
   mPhones   = new DMCC_phone[mPHONES_MAX];

   mService  =null ;
   mConnected=false ;
}  

/*******************************************************************************/
/*                                                                             */
/*                             Деструктор                                      */

  ~DMCC_service()
{
    ReleaseResources();
}

  void IDisposable.Dispose()
{
    ReleaseResources() ;
} 

 private void ReleaseResources()
{
    Disconnect() ;
}

/*******************************************************************************/
/*                                                                             */
/*                            Соединение с сервером                            */

  public int Connect()
{
  const    int   _SESSION_CLEANUP_DELAY                =  60 ;
  const    int   _SESSION_DURATION                     = 180 ;
//const string  _PROTOCOL_VERSION                      ="http://www.ecma-international.org/standards/ecma-323/csta/ed3/priv5" ; //6.1
  const string  _PROTOCOL_VERSION                      = ServiceProvider.DmccProtocolVersion.PROTOCOL_VERSION_6_1 ;
  const   bool    _SECURE                              = false ;
  const   bool     _START_AUTO_KEEP_ALIVE              = true ;
  const   bool     _ALLOW_SERTIFICATE_HOSTNAME_MISMATCH= true ;
  const object      _USER_STATE                        = null ;

           int  InvokeId ;

/*----------------------------------------------------------------- Подготовка */

  if(mService==null) {

      mService                                         =new ServiceProvider() ;
      mService.OnStartApplicationSessionResponse      +=new StartApplicationSessionResponseHandler      (iOnStartApplicationSessionResponse      ) ;
      mService.OnStopApplicationSessionResponse       +=new StopApplicationSessionResponseHandler       (iOnStopApplicationSessionResponse       ) ;
      mService.OnGetDeviceIdListEvent                 +=new GetDeviceIdListEventHandler                 (iOnGetDeviceIdListEvent                 ) ;
      mService.OnGetDeviceIdListResponse              +=new GetDeviceIdListResponseHandler              (iOnGetDeviceIdListResponse              ) ;
      mService.OnGetMonitorListEvent                  +=new GetMonitorListEventHandler                  (iOnGetMonitorListEvent                  ) ;
      mService.OnGetMonitorListResponse               +=new GetMonitorListResponseHandler               (iOnGetMonitorListResponse               ) ;
      mService.OnGetPhysicalDeviceInformationResponse +=new GetPhysicalDeviceInformationResponseHandler (iOnGetPhysicalDeviceInformationResponse ) ;
      mService.OnGetPhysicalDeviceNameResponse        +=new GetPhysicalDeviceNameResponseHandler        (iOnGetPhysicalDeviceNameResponse        ) ;
      mService.OnGetSessionIdListResponse             +=new GetSessionIdListResponseHandler             (iOnGetSessionIdListResponse             ) ;
      mService.OnMissedAtLeastOneKeepAliveEvent       +=new MissedAtLeastOneKeepAliveEventHandler       (iOnMissedAtLeastOneKeepAliveEvent       ) ;
      mService.OnResetApplicationSessionResponse      +=new ResetApplicationSessionResponseHandler      (iOnResetApplicationSessionResponse      ) ;
      mService.OnServerConnectionDownEvent            +=new ServerConnectionDownEventHandler            (iOnServerConnectionDownEvent            ) ;
      mService.OnServerConnectionNotActiveEvent       +=new ServerConnectionNotActiveEventHandler       (iOnServerConnectionNotActiveEvent       ) ;
      mService.OnSessionManagementStartMonitorResponse+=new SessionManagementStartMonitorResponseHandler(iOnSessionManagementStartMonitorResponse) ;
      mService.OnSessionManagementStopMonitorResponse +=new SessionManagementStopMonitorResponseHandler (iOnSessionManagementStopMonitorResponse ) ;
      mService.OnSetSessionCharacteristicsResponse    +=new SetSessionCharacteristicsResponseHandler    (iOnSetSessionCharacteristicsResponse    ) ;
      mService.OnTransferMonitorObjectsEvent          +=new TransferMonitorObjectsEventHandler          (iOnTransferMonitorObjectsEvent          ) ;
      mService.OnTransferMonitorObjectsResponse       +=new TransferMonitorObjectsResponseHandler       (iOnTransferMonitorObjectsResponse       ) ;
      mService.OnChangeDeviceSecurityCodeResponse     +=new ChangeDeviceSecurityCodeResponseHandler     (iOnChangeDeviceSecurityCodeResponse     ) ;
      mService.OnValidateDeviceSecurityCodeResponse   +=new ValidateDeviceSecurityCodeResponseHandler   (iOnValidateDeviceSecurityCodeResponse   ) ;

      mService.getThirdPartyCallController.OnGetThirdPartyDeviceIdResponse +=new GetThirdPartyDeviceIdResponseHandler            (iOnGetThirdPartyDeviceIdResponse ) ;
      mService.getThirdPartyCallController.OnMakeCallResponse              +=new MakeCallResponseHandler                         (iOnMakeCallResponse              ) ;
      mService.getThirdPartyCallController.OnStartMonitorResponse          +=new ThirdPartyCallControlStartMonitorResponseHandler(iOnStartMonitorResponse          ) ;
      mService.getThirdPartyCallController.OnStopMonitorResponse           +=new ThirdPartyCallControlStopMonitorResponseHandler (iOnStopMonitorResponse           ) ;
      mService.getThirdPartyCallController.OnMonitorStopEvent              +=new MonitorStopEventHandeler                        (iOnMonitorStopEvent              ) ;
      mService.getThirdPartyCallController.OnHoldCallResponse              +=new HoldCallResponseHandler                         (iOnHoldCallResponse              ) ;
      mService.getThirdPartyCallController.OnTransferCallResponse          +=new TransferCallResponseHandler                     (iOnTransferCallResponse          ) ;
      mService.getThirdPartyCallController.OnSingleStepTransferCallResponse+=new SingleStepTransferCallResponseHandler           (iOnSingleStepTransferCallResponse) ;
      mService.getThirdPartyCallController.OnGenerateDigitsResponse        +=new GenerateDigitsResponseHandler                   (iOnGenerateDigitsResponse        ) ;
      mService.getThirdPartyCallController.OnEnteredDigitsEvent            +=new EnteredDigitsEventHandler                       (iOnEnteredDigitsEvent            ) ;
      mService.getThirdPartyCallController.OnClearCallResponse             +=new ClearCallResponseHandler                        (iOnClearCallResponse             ) ;

      mService.getThirdPartyCallController.OnCallClearedEvent             +=new CallClearedEventHandeler     (iOnCallClearedEvent      ) ;
      mService.getThirdPartyCallController.OnConferencedEvent             +=new ConferencedEventHandler      (iOnConferencedEvent      ) ;
      mService.getThirdPartyCallController.OnConnectionClearedEvent       +=new ConnectionClearedEventHandler(iOnConnectionClearedEvent) ;
      mService.getThirdPartyCallController.OnDeliveredEvent               +=new DeliveredEventHandler        (iOnDeliveredEvent        ) ;
      mService.getThirdPartyCallController.OnDivertedEvent                +=new DivertedEventHandler         (iOnDivertedEvent         ) ;
      mService.getThirdPartyCallController.OnEstablishedEvent             +=new EstablishedEventHandler      (iOnEstablishedEvent      ) ;
      mService.getThirdPartyCallController.OnFailedEvent                  +=new FailedEventHandler           (iOnFailedEvent           ) ;
      mService.getThirdPartyCallController.OnHeldEvent                    +=new HeldEventHandler             (iOnHeldEvent             ) ;
      mService.getThirdPartyCallController.OnNetworkReachedEvent          +=new NetworkReachedEventHandler   (iOnNetworkReachedEvent   ) ;
      mService.getThirdPartyCallController.OnOriginatedEvent              +=new OriginatedEventHandler       (iOnOriginatedEvent       ) ;
      mService.getThirdPartyCallController.OnQueuedEvent                  +=new QueuedEventHandler           (iOnQueuedEvent           ) ;
      mService.getThirdPartyCallController.OnRetrievedEvent               +=new RetrievedEventHandler        (iOnRetrievedEvent        ) ;
      mService.getThirdPartyCallController.OnServiceInitiatedEvent        +=new ServiceInitiatedEventHandler (iOnServiceInitiatedEvent ) ;
      mService.getThirdPartyCallController.OnTransferredEvent             +=new TransferredEventHandler      (iOnTransferredEvent      ) ;
                     }
/*-------------------------------------------- Закрытие предыдущего соединения */
 
  if(mConnected)  Disconnect() ;

/*------------------------------------------------------ Соединение с сервером */

                    mConnected=false ;
                        mError=null ;

   try 
   {
         InvokeId=mService.StartApplicationSession(mServiceIP, mServicePort, mApplication, mUserName, mUserPassword,
                                                    _SESSION_CLEANUP_DELAY, _SESSION_DURATION,      _PROTOCOL_VERSION,
                                                                   _SECURE,       _USER_STATE, _START_AUTO_KEEP_ALIVE,
                                                                                 _ALLOW_SERTIFICATE_HOSTNAME_MISMATCH );
      if(InvokeId==-1) {
                           mError="StartApplicationSession failed" ;
                           return(-1) ;
                       }
      else             {

                           WaitConnect(InvokeId, 0) ;

                         if(mError!=null)  return(-1) ;
                       }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                          return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

    return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Завершение соединения с сервером                            */

  public int Disconnect()
{
   int  InvokeId ;


          if(!mConnected)  return(0) ;

   try 
   { 
                      mError=null ;

       InvokeId=mService.StopApplicationSession("", null) ;

            WaitResponse(InvokeId, 0) ;

                       if(mError!=null)  return(-1) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                             return(-1) ;
   }

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Создание виртуального телефона                            */

  public int CreatePhone(string pExtension, string pSwitchName, string pSwitchIP, string pPassword)
{
   int  status ;
   int  idx ;

/*----------------------------------------------------------------- Подготовка */

          if(!mConnected)  return(0) ;

/*------------------------------------------------- Создание нового устройства */

         idx=this.iGetFreePhone() ;
      if(idx<0) {
                        mError="All phones slots is busy" ;
                          return(-1) ;
               }

          mPhones[idx].handle=mService.GetNewDevice() ;
          mPhones[idx].handle.OnGetDeviceIdResponse    +=new GetDeviceIdResponseHandler    (iOnGetDeviceIdResponse    ) ;
          mPhones[idx].handle.OnReleaseDeviceIdResponse+=new ReleaseDeviceIdResponseHandler(iOnReleaseDeviceIdResponse) ;

          mPhones[idx].Extension=pExtension ;
          mPhones[idx].Switch   =pSwitchName ;

/*------------------------------------------------------ Запрос идентификатора */

   try 
   { 
       mPhones[idx].LastError=null ;
                       status=  0 ;

       mPhones[idx].InvokeId=mPhones[idx].handle.GetDeviceId(pExtension, pSwitchName, pSwitchIP, false, null) ;

           status=WaitResponse(mPhones[idx].InvokeId, 0) ;

        if(                status==  0 && 
           mPhones[idx].LastError!=null  ) {
                        mError=mPhones[idx].LastError ;
                        status= -1 ;
                                           }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                        status= -1 ;
   }
/*------------------------------------------------------ Регистрация терминала */

          mPhones[idx].handle.getPhone.OnRegisterTerminalResponse  += new RegisterTerminalResponseHandler(iOnRegisterTerminalResponse);
          mPhones[idx].handle.getPhone.OnUnregisterTerminalResponse+= new UnregisterTerminalResponseHandler(iOnUnregisterTerminalResponse);
          mPhones[idx].handle.getPhone.OnTerminalUnregisteredEvent += new TerminalUnregisteredEventHandler(iOnTerminalUnregisteredEvent);

   try 
   { 
       Phone.LoginInfo  loginInfo;
       Phone.MediaInfo  mediaInfo;

                loginInfo = new Phone.LoginInfo();
                mediaInfo = new Phone.MediaInfo();

                loginInfo.ExtensionPassword      =pPassword;
                loginInfo.ForceLogin             = true;                
                mediaInfo.MediaControl           =Media.MediaMode.SERVER_MODE;
                mediaInfo.RequestedDependencyMode=Phone.MediaInfo.DependencyMode.Main;

       mPhones[idx].LastError=null ;
                       status=  0 ;

       mPhones[idx].InvokeId=mPhones[idx].handle.getPhone.RegisterTerminal(loginInfo, mediaInfo, null) ;

           status=WaitResponse(mPhones[idx].InvokeId, 0) ;

        if(                status==  0 && 
           mPhones[idx].LastError!=null  ) {
                        mError=mPhones[idx].LastError ;
                        status= -1 ;
                                           }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                        status= -1 ;
   }

/*-----------------------------------------------------------------------------*/

  if(status!=0) {
                   DeletePhone(idx) ;
                         return(-1) ;
                }

   return(idx) ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Освобождение виртуального телефона                        */

  public void  DeletePhone(int pIdx)
{
/*----------------------------------------------------------- Входной контроль */

    if(        pIdx <   0 )  return ;

    if(mPhones[pIdx]==null)  return ;

/*----------------------------------------------------------- Отключение Media */

              MediaRelease(pIdx) ;

/*------------------------------------------------------- Отключение терминала */

  if(mPhones[pIdx].handle.getPhone.getRegistered) {

     try 
     { 
         mPhones[pIdx].LastError=null ;

         mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.UnregisterTerminal(null) ;

            WaitResponse(mPhones[pIdx].InvokeId, 0) ;
     }
     catch (Exception exc)
     {
                        mError=exc.Message ;
     }
                                                  }
/*------------------------------------------------ Освобождение идентификатора */

  if(mPhones[pIdx].DeviceId!="") {

     try 
     { 
         mPhones[pIdx].LastError=null ;

         mPhones[pIdx].InvokeId=mPhones[pIdx].handle.ReleaseDeviceId(null) ;

            WaitResponse(mPhones[pIdx].InvokeId, 0) ;
     }
     catch (Exception exc)
     {
                        mError=exc.Message ;
     }

                                       mPhones[pIdx].DeviceId=null ;
                                 }
/*---------------------------------------------------- Освобождение устройства */

               mPhones[pIdx].handle    =null ;
               mPhones[pIdx].Extension =null ;
               mPhones[pIdx].Switch    =null ;
               mPhones[pIdx].DeviceId_3=null ;

         iReleasePhone(pIdx) ;

/*-----------------------------------------------------------------------------*/

   return ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Выполнение звонка с виртуального телефона                 */

  public string  MakeCall(int pIdx, string pDestination)
{
                                                    string  Source_3 ;
                                                    string  Destination_3 ;
                                                       int  status ;
   ThirdPartyCallController.ThirdPartyPerCallControlEvents  EventsFlags ;
                   ThirdPartyCallController.CallIdentifier  CallId ;

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(null) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(null) ;
                                      }

    if(mPhones[pIdx].transfer   ==false &&
       mPhones[pIdx].ConGlobalId!= null   ) {
                                               mError="Previous call not completed" ;
                                                 return(null) ;
                                            }
/*------------------------------------------------------ Освобождение ресурсов */

  if(mPhones[pIdx].transfer==false) {

                   mPhones[pIdx].CallStatus              =null ;
                   mPhones[pIdx].ConLocalId              =null ;
                   mPhones[pIdx].ConGlobalId             =null ;
                   mPhones[pIdx].CallMonitorId           =null ;
                   mPhones[pIdx].MediaMonitorId          =null ;
                   mPhones[pIdx].LastError               =null ;

                   mPhones[pIdx].Times.InitTime          =DateTime.Now ;
                   mPhones[pIdx].Times.DeliveryTime      =DateTime.MinValue ;
                   mPhones[pIdx].Times.ConnectTime       =DateTime.MinValue ;
                   mPhones[pIdx].Times.ClearTime         =DateTime.MinValue ;

                   mPhones[pIdx].Events.Failed           =false ;
                   mPhones[pIdx].Events.Failed_t         =false ;
                   mPhones[pIdx].Events.NetworkReached   =false ;
                   mPhones[pIdx].Events.Delivered        =false ;
                   mPhones[pIdx].Events.Delivered_t      =false ;
                   mPhones[pIdx].Events.Established      =false ;
                   mPhones[pIdx].Events.Established_t    =false ;
                   mPhones[pIdx].Events.Transfered       =false ;
                   mPhones[pIdx].Events.CallCleared      =false ;
                   mPhones[pIdx].Events.ConnectionCleared=false ;
                   mPhones[pIdx].Events.MonitorStopped   =false ;
                   mPhones[pIdx].Events.MediaTonesFlushed=false ;
                                    }
/*------------------------------------------- Запрос идентификатора 3-rd party */

  if(mPhones[pIdx].DeviceId_3==null) {
          
   try 
   { 
       mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.GetThirdPartyDeviceId(mPhones[pIdx].Switch, mPhones[pIdx].Extension, null) ;

           status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

        if(                 status!= 0  || 
           mPhones[pIdx].LastError!=null  ) {
                        mError=mPhones[pIdx].LastError ;
                                         return(null) ;
                                            }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(null) ;
   }

                                     }
/*-------------------------- Формирование идентификатора 3-rd party для "цели" */

            Source_3=mPhones[pIdx].DeviceId_3 ;
       Destination_3=pDestination+Source_3.Substring(Source_3.IndexOf(":")) ;

/*---------------------------------------------------------- Выполнение звонка */
          
   try 
   { 
       mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.MakeCall(Source_3, Destination_3, null) ;

           status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

        if(                 status!= 0  || 
           mPhones[pIdx].LastError!=null  ) {
                        mError=mPhones[pIdx].LastError ;
                                         return(null) ;
                                            }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(null) ;
   }
/*----------------------------------------------------- Запуск монитора звонка */

          EventsFlags                       = new ThirdPartyCallController.ThirdPartyPerCallControlEvents(true) ;
          EventsFlags.CallClearedEvent      =true ;
          EventsFlags.ConferencedEvent      =true ;
          EventsFlags.ConnectionClearedEvent=true ;
          EventsFlags.DeliveredEvent        =true ;
          EventsFlags.DivertedEvent         =true ;
          EventsFlags.EstablishedEvent      =true ;
          EventsFlags.FailedEvent           =true ;
          EventsFlags.HeldEvent             =true ;
          EventsFlags.NetworkReachedEvent   =true ;
          EventsFlags.OriginatedEvent       =true ;
          EventsFlags.QueuedEvent           =true ;
          EventsFlags.RetrievedEvent        =true ;
          EventsFlags.ServiceInitiatedEvent =true ;
          EventsFlags.TransferredEvent      =true ;

   try 
   { 
        if(mPhones[pIdx].transfer==false)  CallId=mPhones[pIdx].ConLocalId ;   
        else                               CallId=mPhones[pIdx].ConLocalId_t ;

             mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.StartMonitor(CallId, EventsFlags, null) ;

           status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

        if(                 status!= 0  || 
           mPhones[pIdx].LastError!=null  ) {
                                         mError=mPhones[pIdx].LastError ;
                      mPhones[pIdx].ConGlobalId=null ;
                                         return(null) ;
                                            }
   }
   catch (Exception exc)
   {
                                mError=exc.Message ;
             mPhones[pIdx].ConGlobalId=null ;
                                return(null) ;
   }
/*-----------------------------------------------------------------------------*/

  if(mPhones[pIdx].transfer==true)  return(mPhones[pIdx].ConGlobalId_t) ;
  else                              return(mPhones[pIdx].ConGlobalId) ;
}
/*******************************************************************************/
/*                                                                             */
/*                  Прекращение звонка с виртуального телефона                 */

  public int  DropCall(int pIdx)
{
/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   )  return(0) ;

    if(mPhones[pIdx].ConGlobalId==null)  return(0) ;

/*--------------------------------------------------------------- Сброс звонка */
          
   try 
   { 
          mService.getThirdPartyCallController.ClearCall(mPhones[pIdx].ConLocalId, null) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Отыгрыш цифр на виртуальном телефоне                      */

  public int  DigitsToCall(int pIdx, string pDigits, bool pWait)
{
           int  status ;

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(-1) ;
                                      }

    if(mPhones[pIdx].ConGlobalId==null) {
                                          mError="Call not initiated" ;
                                            return(-1) ;
                                        }
/*--------------------------------------------- Направление команды на отыгрыш */

                   mPhones[pIdx].LastError=null ;

   try 
   {
       mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.GenerateDigits(mPhones[pIdx].ConLocalId, pDigits, null) ;

     if(pWait) {
                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
               }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*              Перенаправление звонка на виртуальном телефоне                 */

  public int  TransferCall(int pIdx, string pTarget, string pStage)
{
  string  link ;
     int  status ;

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(-1) ;
                                      }

    if(mPhones[pIdx].ConGlobalId==null) {
                                          mError="Call not initiated" ;
                                            return(-1) ;
                                        }
/*------------------------------------------------------- Подготовка трансфера */

  if(String.Compare(pStage, "Prepare",  true)==0)
  {
/*- - - - - - - - - - - - - - - - - - - - - - - - -  Постановка звонка на Hold */

    iLog("TransferCall - HoldCall...");

                   mPhones[pIdx].LastError=null ;

   try 
   {
       mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.HoldCall(mPhones[pIdx].ConLocalId, null) ;

                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*- - - - - - - - - - - - - - - - - - - - - - -  Инициация transfer-соединения */
    iLog("TransferCall - MakeCall...");

                    mPhones[pIdx].transfer=true ;
              link=MakeCall(pIdx, pTarget) ;
                    mPhones[pIdx].transfer=false ;

    iLog("TransferCall - MakeCall error: "+mError);

            if(link==null)  return(-1) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         return(0) ;
  }
/*-------------------------------------------------------- Стыковка соединений */

  else
  if(String.Compare(pStage, "Transfer",  true)==0)
  {

       iLog("TransferCall - TransferCall...");

   try 
   {
       mPhones[pIdx].InvokeId=mService.getThirdPartyCallController.TransferCall(mPhones[pIdx].ConLocalId, mPhones[pIdx].ConLocalId_t, null) ;

                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }

         return(0) ;
  }
/*----------------------------------------------------------- Отмена трансфера */

  else
  if(String.Compare(pStage, "Drop",  true)==0)
  {

       iLog("TransferCall - DropCall...");
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - Остановка монитора */
   try 
   { 
          mService.getThirdPartyCallController.StopMonitor(mPhones[pIdx].CallMonitorId_t, null) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
//                             return(-1) ;
   }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Сброс звонка */         
   try 
   { 
          mService.getThirdPartyCallController.ClearCall(mPhones[pIdx].ConLocalId_t, null) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
//                             return(-1) ;
   }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         return(0) ;
  }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Подготовка к использованию Media-возможностей             */

  public int  MediaInitialize(int pIdx, bool pWait)
{
                       int  status ;
         Media.MediaEvents  EventsFlags ;

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(-1) ;
                                      }

    if(mPhones[pIdx].ConGlobalId==null) {
                                          mError="Call not initiated" ;
                                            return(-1) ;
                                        }
/*------------------------------------- Регистрация обработчиков Media-событий */

          mPhones[pIdx].handle.getPhone.getMedia.OnStartMonitorResponse            += new MediaStartMonitorResponseHandler(iOnMediaStartMonitorResponse);
          mPhones[pIdx].handle.getPhone.getMedia.OnStopMonitorResponse             += new MediaStopMonitorResponseHandler(iOnMediaStopMonitorResponse);
          mPhones[pIdx].handle.getPhone.getMedia.OnStartToneCollectionResponse     += new StartToneCollectionResponseHandler(iOnMediaStartToneCollectionResponse);
          mPhones[pIdx].handle.getPhone.getMedia.OnStopToneCollectionResponse      += new StopToneCollectionResponseHandler(iOnMediaStopToneCollectionResponse);
          mPhones[pIdx].handle.getPhone.getMedia.OnSetToneRetrievalCriteriaResponse+= new SetToneRetrievalCriteriaResponseHandler(iOnMediaSetToneRetrievalCriteriaResponse);
          mPhones[pIdx].handle.getPhone.getMedia.OnStartPlayingResponse            += new StartPlayingResponseHandler(iOnMediaStartPlayingResponse);

          mPhones[pIdx].handle.getPhone.getMedia.OnMediaStartedEvent      += new MediaStartedEventHandler(iOnMediaStartedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnMediaStoppedEvent      += new MediaStoppedEventHandler(iOnMediaStoppedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnPlayingEvent           += new PlayingEventHandler(iOnMediaPlayingEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnPlayingStoppedEvent    += new PlayingStoppedEventHandler(iOnMediaPlayingStoppedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnPlayingSuspendedEvent  += new PlayingSuspendedEventHandler(iOnMediaPlayingSuspendedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnRecordingEvent         += new RecordingEventHandler(iOnMediaRecordingEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnRecordingStoppedEvent  += new RecordingStoppedEventHandler(iOnMediaRecordingStoppedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnRecordingSuspendedEvent+= new RecordingSuspendedEventHandler(iOnMediaRecordingSuspendedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnToneDetectedEvent      += new ToneDetectedEventHandler(iOnMediaToneDetectedEvent);
          mPhones[pIdx].handle.getPhone.getMedia.OnTonesRetrievedEvent    += new TonesRetrievedEventHandler(iOnMediaTonesRetrievedEvent);

/*---------------------------------------------- Запуск монитора Media-событий */

          EventsFlags                        = new Media.MediaEvents(true) ;
          EventsFlags.MediaStartedEvent      =true ;
          EventsFlags.MediaStoppedEvent      =true ;
          EventsFlags.PlayingEvent           =true ;
          EventsFlags.PlayingStoppedEvent    =true ;
          EventsFlags.PlayingSuspendedEvent  =true ;
          EventsFlags.RecordingEvent         =true ;
          EventsFlags.RecordingSuspendedEvent=true ;
          EventsFlags.TonesDetectedEvent     =true ;
          EventsFlags.TonesRetrievedEvent    =true ;

                   mPhones[pIdx].LastError=null ;

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StartMonitor(EventsFlags, null) ;

     if(pWait) {
                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
               }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*              Завершение использования Media-возможностей                    */

  public void  MediaRelease(int pIdx)
{
/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return ;
                                      }

    if(mPhones[pIdx].MediaMonitorId==null) {
                                              mError="Media not initiated" ;
                                                return ;
                                           }

                   mPhones[pIdx].LastError=null ;

/*----------------------------------------- Остановка сборщика тонового набора */

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StopToneCollection(null) ;

//               WaitResponse(mPhones[pIdx].InvokeId, 0) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
   }
/*---------------------------------------------------- Остановка проигрывателя */

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StopPlaying(null) ;

//               WaitResponse(mPhones[pIdx].InvokeId, 0) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
   }
/*------------------------------------------- Остановка монитора Media-событий */

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StopMonitor(mPhones[pIdx].MediaMonitorId, null) ;

                 WaitResponse(mPhones[pIdx].InvokeId, 0) ;

   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
   }
/*-----------------------------------------------------------------------------*/

   return ;
}
/*******************************************************************************/
/*                                                                             */
/*              Включение режима ожидания тонального набора                    */

  public int  MediaWaitTones(int pIdx, int pCnt, char pEndChar, bool pWait)
{
   Media.RetrievalCriteria  Criteria ;
                       int  status ;

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(-1) ;
                                      }

    if(mPhones[pIdx].MediaMonitorId==null) {
                                              mError="Media not initiated" ;
                                                return(-1) ;
                                           }

/*--------------------------------------------- Инициализация статусных данных */

                 mPhones[pIdx].LastError               =null ;

                 mPhones[pIdx].Events.MediaTonesFlushed=false ;
                 mPhones[pIdx].Events.MediaTones       =null ;

/*-------------------------------- Задание критерия фильтрации тонового набора */

                 Criteria                  = new Media.RetrievalCriteria() ;
                 Criteria.NumberOfTones    = pCnt ;
                 Criteria.InitialTimeout   =-1 ;
                 Criteria.InterDigitTimeout=-1 ;
                 Criteria.FlushCharacter   =pEndChar ;

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.SetToneRetrievalCriteria(Criteria, null) ;

     if(pWait) {
                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
               }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-------------------------------------------- Запуск сборщика тонового набора */

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StartToneCollection(null) ;

     if(pWait) {
                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
               }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                         Запуск проигрыша аудио-файла                        */

  public int  MediaPlay(int pIdx, string pAudioFile, bool pToneTermination, bool pWait)
{
           List<string>  FileNameList ;
   Media.PlayRepeatInfo  PlayRepeatInfo ;
                    int  status ;

/*----------------------------------------------------------- Входной контроль */

                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(-1) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                           return(-1) ;
                                      }

    if(mPhones[pIdx].MediaMonitorId==null) {
                                              mError="Media not initiated" ;
                                                return(-1) ;
                                           }

/*--------------------------------------------- Инициализация статусных данных */

                 mPhones[pIdx].LastError=null ;
                 
/*------------------------------------------- Запуск проигрыша звукового файла */

                     FileNameList=new List<string>() ;
                     FileNameList.Add(pAudioFile) ;

                     PlayRepeatInfo             =new Media.PlayRepeatInfo() ;
                     PlayRepeatInfo.PlayCount   = 1 ;
                     PlayRepeatInfo.PlayInterval=-1 ;

   try 
   {
       mPhones[pIdx].InvokeId=mPhones[pIdx].handle.getPhone.getMedia.StartPlaying(FileNameList, pToneTermination, PlayRepeatInfo, null) ;

     if(pWait) {
                   status=WaitResponse(mPhones[pIdx].InvokeId, 0) ;

            if(                 status!= 0  || 
               mPhones[pIdx].LastError!=null  ) {
                                  mError=mPhones[pIdx].LastError ;
                                                     return(-1) ;
                                                }
               }
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
                                return(-1) ;
   }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*              Включение режима ожидания тонального набора                    */

  public string  MediaGetTones(int pIdx)
{
  string  tones ;   

/*----------------------------------------------------------- Входной контроль */
    
                                        mError=null ;

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                        mError="Invalid virtual phone index" ;
                                           return(null) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                         mError="Virtual phone not created" ;
                                            return(null) ;
                                      }

    if(mPhones[pIdx].MediaMonitorId==null) {
                                              mError="Media not initiated" ;
                                                 return(null) ;
                                           }
/*---------------------------------------------- Сброс статуса тонового набора */

          tones=mPhones[pIdx].Events.MediaTones ;

                mPhones[pIdx].Events.MediaTones       =null ;
                mPhones[pIdx].Events.MediaTonesFlushed=false ;

/*----------------------------------------- Остановка сборщика тонового набора */

   try 
   {
         mPhones[pIdx].handle.getPhone.getMedia.StopToneCollection(null) ;
   }
   catch (Exception exc)
   {
                        mError=exc.Message ;
   }
/*-----------------------------------------------------------------------------*/

   return(tones) ;
}
/*******************************************************************************/
/*                                                                             */
/*               Запрос состояния звонка с виртуального телефона               */

  public string  GetCallStatus(int pIdx)
{
/*----------------------------------------------------------- Входной контроль */

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                           return(null) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                           return(null) ;
                                      }
/*------------------------------------------------------------- Выдача статуса */

       return(mPhones[pIdx].CallStatus) ;

/*-----------------------------------------------------------------------------*/
}

  public string  GetCallStatus(int pIdx, ref DMCC_phone_times times, ref DMCC_phone_events events)
{
/*----------------------------------------------------------- Входной контроль */

    if(pIdx<0 || pIdx>=mPHONES_MAX) {
                                           return(null) ;
                                    }

    if(mPhones[pIdx]       ==null ||
       mPhones[pIdx].handle==null   ) {
                                           return(null) ;
                                      }
/*------------------------------------------------------------- Выдача статуса */

        times=mPhones[pIdx].Times ;
       events=mPhones[pIdx].Events ;

       return(mPhones[pIdx].CallStatus) ;

/*-----------------------------------------------------------------------------*/
}
/*******************************************************************************/
/*                                                                             */
/*                   Ожидание отработки запросов                               */

  public int  WaitConnect(int pWaitInvokeId, int  pTimeOutMax)
{
   int  time_out ;

/*----------------------------------------------------------------- Подготовка */

           if(pTimeOutMax<=  0)  pTimeOutMax =mTimeOutDefault ;
           if(pTimeOutMax< 100)  pTimeOutMax*= 1000 ;

/*------------------------------------- Подготовка ожидания конкретного ответа */

           if(pWaitInvokeId>0) {
                                  mWaitReply=  true ;
                                  mWaitID   =pWaitInvokeId ;
                               }
/*--------------------------------------------------------- Ожидание обработки */

                              time_out=0 ;

    while(true) {

                if(pWaitInvokeId>0 && mWaitReply==false) {                    /* Если пришел конкретный ответ... */
                                                           mWaitID=0 ;
                                                              break ;
                                                         }

                       Thread.Sleep(mTimeOutQuant) ;

                   time_out+=mTimeOutQuant ;                                  /* Проверка времени ожидания ответов... */
                if(time_out>=pTimeOutMax) {
                                            mError="TimeOut is expired" ;
                                               return(-1) ;
                                          }
                }
/*-----------------------------------------------------------------------------*/

    return(0) ;
}

  public int  WaitResponse(int pWaitInvokeId, int  pTimeOutMax)
{
   int  time_out ;

/*----------------------------------------------------------------- Подготовка */

           if(!mConnected) {
                             mError="Not connected to AES" ;
                                            return(-1) ;
                           }

           if(pTimeOutMax<=  0)  pTimeOutMax =mTimeOutDefault ;
           if(pTimeOutMax< 100)  pTimeOutMax*= 1000 ;

/*------------------------------------- Подготовка ожидания конкретного ответа */

           if(pWaitInvokeId>0) {
                                  mWaitReply=  true ;
                                  mWaitID   =pWaitInvokeId ;
                               }
/*--------------------------------------------------------- Ожидание обработки */

                              time_out=0 ;

    while(true) {

                if(pWaitInvokeId>0 && mWaitReply==false) {                    /* Если пришел конкретный ответ... */
                                                           mWaitID=0 ;
                                                              break ;
                                                         }

                if(!mConnected) {                                             /* Если нарушено соединение... */  
                                  mError="Connected to AES is broken" ;
                                                  return(-1) ;
                                }

                       Thread.Sleep(mTimeOutQuant) ;

                   time_out+=mTimeOutQuant ;                                  /* Проверка времени ожидания ответов... */
                if(time_out>=pTimeOutMax) {
                                            mError="TimeOut is expired" ;
                                               return(-1) ;
                                          }
                }
/*-----------------------------------------------------------------------------*/

    return(0) ;
}

  public int  WaitPhones(int  pTimeOutMax)
{
   int  time_out ;
   int  i ;

/*----------------------------------------------------------------- Подготовка */

           if(!mConnected) {
                             mError="Not connected to AES" ;
                                            return(-1) ;
                           }

           if(pTimeOutMax<=  0)  pTimeOutMax =mTimeOutDefault ;
           if(pTimeOutMax< 100)  pTimeOutMax*= 1000 ;

/*--------------------------------------------------------- Ожидание обработки */

                              time_out=0 ;

    while(true) {

                if(!mConnected) {                                             /* Если нарушено соединение... */  
                                  mError="Connected to AES is broken" ;
                                                  return(-1) ;
                                }

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null && mPhones[i].InvokeId!=0)  break ;

                if(i>=mPHONES_MAX)  break ;

                       Thread.Sleep(mTimeOutQuant) ;

                   time_out+=mTimeOutQuant ;                                  /* Проверка времени ожидания ответов... */
                if(time_out>=pTimeOutMax) {
                                            mError="TimeOut is expired" ;
                                               return(-1) ;
                                          }
                }
/*-----------------------------------------------------------------------------*/

    return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушка события подтверждения подключения                       */

  void  iOnStartApplicationSessionResponse(object sender, ServiceProvider.StartApplicationSessionResponseArgs e)
{
    iLog("OnStartApplicationSessionResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getInvokeId!=mWaitID)  return ;

    iLog("Catch Connection!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getSessionId!="") {
                                  mSessionID=e.getSessionId ;
                                  mConnected=   true ;
                            }
     else                   {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                            }

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушка события подтверждения отключения                        */

  void  iOnStopApplicationSessionResponse(object sender, ServiceProvider.StopApplicationSessionResponseArgs e)
{
    iLog("OnStopApplicationSessionResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch DisConnection!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             mConnected=false ;
             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий запроса списка устройств                        */

  void  iOnGetDeviceIdListResponse(object sender, ServiceProvider.GetDeviceIdListResponseArgs e)
{
    iLog("GetDeviceIdListResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch DeviceIdList!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             mWaitReply=false ;

             iLogAndWait("\nUNHANDLED");
}
  void  iOnGetDeviceIdListEvent(object sender, ServiceProvider.GetDeviceIdListEventArgs e)
{
    iLog("GetDeviceIdListEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий запроса списка мониторов                        */

  void  iOnGetMonitorListResponse(object sender, ServiceProvider.GetMonitorListResponseArgs e)
{
    iLog("GetMonitorListResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch MonitorList!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
  void  iOnGetMonitorListEvent(object sender, ServiceProvider.GetMonitorListEventArgs e)
{
    iLog("GetDeviceIdListEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушка события выдачи информации о физическом устройстве       */

  void  iOnGetPhysicalDeviceInformationResponse(object sender, ServiceProvider.GetPhysicalDeviceInformationResponseArgs e)
{
    iLog("OnGetPhysicalDeviceInformationResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch GetPhysicalDeviceInformation!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушка события выдачи имени физического устройства             */

  void  iOnGetPhysicalDeviceNameResponse(object sender, ServiceProvider.GetPhysicalDeviceNameResponseArgs e)
{
    iLog("OnGetPhysicalDeviceNameResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch GetPhysicalDeviceNameResponse!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий запроса списка сессий                           */

  void  iOnGetSessionIdListResponse(object sender, ServiceProvider.GetSessionIdListResponseArgs e)
{
    iLog("GetSessionIdListResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch SessionIdList!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий пропуска ALIVE-сигнала                          */

  void  iOnMissedAtLeastOneKeepAliveEvent(object sender, ServiceProvider.MissedAtLeastOneKeepAliveEventArgs e)
{
    iLog("MissedAtLeastOneKeepAliveEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий ответа на явный ALIVE-сигнала                   */

  void  iOnResetApplicationSessionResponse(object sender, ServiceProvider.ResetApplicationSessionResponseArgs e)
{
    iLog("ResetApplicationSessionResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch ResetApplicationSession!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий отключения канала связи                         */

  void  iOnServerConnectionDownEvent(object sender, ServiceProvider.ServerConnectionDownEventArgs e)
{
    iLog("ServerConnectionDownEvent");

        mConnected=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий сигнала об отсутствии активности                */

  void  iOnServerConnectionNotActiveEvent(object sender, ServiceProvider.ServerConnectionNotActiveEventArgs e)
{
    iLog("ServerConnectionNotActiveEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий ответа запуска/остановки мониторинга сессии     */

  void  iOnSessionManagementStartMonitorResponse(object sender, ServiceProvider.SessionManagementStartMonitorResponseArgs e)
{
    iLog("SessionManagementStartMonitorResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch SessionManagementStartMonitor!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}

  void  iOnSessionManagementStopMonitorResponse(object sender, ServiceProvider.SessionManagementStopMonitorResponseArgs e)
{
    iLog("SessionManagementStopMonitorResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch SessionManagementStopMonitor!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий ответа изменения параметров сессии              */

  void  iOnSetSessionCharacteristicsResponse(object sender, ServiceProvider.SetSessionCharacteristicsResponseArgs e)
{
    iLog("SetSessionCharacteristicsResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch SetSessionCharacteristicsResponse!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             iLogAndWait("\nUNHANDLED");

             mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий передачи списка мониторов                       */

  void  iOnTransferMonitorObjectsResponse(object sender, ServiceProvider.TransferMonitorObjectsResponseArgs e)
{
    iLog("TransferMonitorObjectsResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if(e.getInvokeId != mWaitID) return;

    iLog("Catch TransferMonitorObjects!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             mWaitReply=false ;

             iLogAndWait("\nUNHANDLED");
}
  void  iOnTransferMonitorObjectsEvent(object sender, ServiceProvider.TransferMonitorObjectsEventArgs e)
{
    iLog("TransferMonitorObjectsEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*       Ловушки событий ответов работы с кодами безопасности устройств        */

  void  iOnChangeDeviceSecurityCodeResponse(object sender, ServiceProvider.ChangeDeviceSecurityCodeResponseArgs e)
{
    iLog("ChangeDeviceSecurityCodeResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if (e.getInvokeId != mWaitID) return;

    iLog("Catch ChangeDeviceSecurityCode!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             mWaitReply=false ;

             iLogAndWait("\nUNHANDLED");
}
  void  iOnValidateDeviceSecurityCodeResponse(object sender, ServiceProvider.ValidateDeviceSecurityCodeResponseArgs e)
{
    iLog("ValidateDeviceSecurityCodeResponse - check reference " + e.getInvokeId + "?" + mWaitID + "");

  if (e.getInvokeId != mWaitID) return;

    iLog("Catch ValidateDeviceSecurityCode!");
  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

     if(e.getError!="") {
                                mError=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(mError==null)  mError="Undefined error" ;
                        }

             mWaitReply=false ;

             iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*          Ловушка события ответа на запрос идентификатора устройства         */

  void  iOnGetDeviceIdResponse(object sender, Device.GetDeviceIdResponseArgs e)
{
  string  Error ;
     int  i ;
 
    iLog("OnGetDeviceIdResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
                             mPhones[i].DeviceId =  null ;
                             mPhones[i].InvokeId =    0 ;
                             mPhones[i].LastError=  null ;

     if(e.getError=="") {
                             mPhones[i].DeviceId =e.getDevice.getDeviceIdAsString ;
                        }
     else               {
                               Error=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушка события освобождения идентификатора устройства          */

  void  iOnReleaseDeviceIdResponse(object sender, Device.ReleaseDeviceIdResponseArgs e)
{
     int  i ;

 
    iLog("OnReleaseDeviceIdResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
                        mPhones[i].DeviceId=null ;
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*          Ловушка события ответа на запрос регистрации терминала             */

  void  iOnRegisterTerminalResponse(object sender, Phone.RegisterTerminalResponseArgs e)
{
  string  Error ;
     int  i ;
 
    iLog("OnRegisterTerminalResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
                             mPhones[i].InvokeId =   0 ;
                             mPhones[i].LastError= null ;
                             mPhones[i].Terminal = false ;

     if(e.getError!="") {
                               Error=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else
     if(e.getCode!="1") {
                             mPhones[i].LastError=e.getReason ;
                        }
     else               {
                             mPhones[i].Terminal = true ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                   Ловушка события отключение терминала                      */

  void  iOnUnregisterTerminalResponse(object sender, Phone.UnregisterTerminalResponseArgs e)
{
     int  i ;

 
    iLog("OnUnregisterTerminalResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
                             mPhones[i].Terminal=false ;
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}

  void  iOnTerminalUnregisteredEvent(object sender, Phone.TerminalUnregisteredEventArgs e)
{
    iLog("TerminalUnregisteredEvent");

    iLogAndWait("\nUNHANDLED");
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события запроса идентификатора 3-rd party           */

  void  iOnGetThirdPartyDeviceIdResponse(object sender, ThirdPartyCallController.GetThirdPartyDeviceIdResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnGetThirdPartyDeviceIdResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<definedError>", "</definedError>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                             mPhones[i].DeviceId_3=e.getDeviceIdAsString ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события выполнения звонка 3-rd party                */

  void  iOnMakeCallResponse(object sender, ThirdPartyCallController.MakeCallResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMakeCallResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {

            if(mPhones[i].transfer==false) {
                             mPhones[i].ConLocalId    =e.getCallingDeviceConnectionId ;
                             mPhones[i].ConGlobalId   =e.getGloballyUniqueCallLinkageId ;
                             mPhones[i].Times.InitTime=DateTime.Now ;
                                          }
            else                          {
                             mPhones[i].ConLocalId_t  =e.getCallingDeviceConnectionId ;
                             mPhones[i].ConGlobalId_t =e.getGloballyUniqueCallLinkageId ;
                                          }
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события запуска монитора соединения                 */

  void  iOnStartMonitorResponse(object sender, ThirdPartyCallController.StartMonitorResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnStartMonitorResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null){
                               Error="Undefined error" ;
                                 iLogException(e.getError) ;
                             }

                             mPhones[i].LastError= Error ;
                        }
     else               {
           if(mPhones[i].transfer==true)  mPhones[i].CallMonitorId_t=e.getMonitorId ; 
           else                           mPhones[i].CallMonitorId  =e.getMonitorId ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события остановки монитора соединения               */

  void  iOnStopMonitorResponse(object sender, ThirdPartyCallController.ThirdPartyCallControlStopMonitorResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnStopMonitorResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                             mPhones[i].CallMonitorId=null ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}

  void  iOnMonitorStopEvent(object sender, ThirdPartyCallController.MonitorStopEventArgs e)
{
     int  i ;
 
    iLog("OnMonitorStopEvent - "+e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected monitor event!");
   else              {
                                               mPhones[i].CallStatus           ="MonitorStopped" ;
                                               mPhones[i].Events.MonitorStopped= true ;
                                               mPhones[i].CallMonitorId        = null ;  
        if(mPhones[i].CallMonitorId==null &&
           mPhones[i].ConGlobalId  ==null   )  mPhones[i].CallStatus    ="ReadyForNext" ;
                     }
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события холдирования звонка                         */

  void  iOnHoldCallResponse(object sender, ThirdPartyCallController.HoldCallResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnHoldCallResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      Error:"+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                    Ловушка события трансфера звонка                         */

  void  iOnTransferCallResponse(object sender, ThirdPartyCallController.TransferCallResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnTransferCallResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      Error:"+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события перенаправления звонка                      */

  void  iOnSingleStepTransferCallResponse(object sender, ThirdPartyCallController.SingleStepTransferCallResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnSingleStepTransferCallResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      Error:"+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события отыгрыша цифр                               */

  void  iOnGenerateDigitsResponse(object sender, ThirdPartyCallController.GenerateDigitsResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnGenerateDigitsResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      Error:"+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}

  void  iOnEnteredDigitsEvent(object sender, ThirdPartyCallController.EnteredDigitsEventArgs e)
{
   int  i ;

    iLog("CALL - EnteredDigits " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else                mPhones[i].CallStatus="Failed" ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события команды сброса звонка                       */

  void  iOnClearCallResponse(object sender, ThirdPartyCallController.ClearCallResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnClearCallResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      Error:"+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события запуска Media-монитора                      */

  void  iOnMediaStartMonitorResponse(object sender, Media.StartMonitorResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaStartMonitorResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null){
                               Error="Undefined error" ;
                                 iLogException(e.getError) ;
                             }

                             mPhones[i].LastError= Error ;
                        }
     else               {
                             mPhones[i].MediaMonitorId=e.getMonitorId ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события остановки Media-монитора                    */

  void  iOnMediaStopMonitorResponse(object sender, Media.MediaStopMonitorResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaStopMonitorResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                             mPhones[i].MediaMonitorId=null ;
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ловушка события задания критериев тонового набора           */

  void  iOnMediaSetToneRetrievalCriteriaResponse(object sender, Media.SetToneRetrievalCriteriaResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaSеtToneRetrievalCriteriaResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*          Ловушка события запуска/остановки сборщика тонового набора         */

  void  iOnMediaStartToneCollectionResponse(object sender, Media.StartToneCollectionResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaStartToneCollectionResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}

  void  iOnMediaStopToneCollectionResponse(object sender, Media.StopToneCollectionResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaStopToneCollectionResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*                Ловушка события запуска проигрыша звукового файла            */

  void  iOnMediaStartPlayingResponse(object sender, Media.StartPlayingResponseArgs e)
{
  string  Error ;
     int  i ;

 
    iLog("OnMediaStartPlayingResponse - check reference "+e.getInvokeId+"?"+mWaitID+"");

  if(e.getError!="")
    iLog("      ERROR! "+e.getError + "");

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].InvokeId==e.getInvokeId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected response!");
   else 
   {
     if(e.getError!="") {
                               Error=iExtract(e.getError, "<systemResourceAvailability>", "</systemResourceAvailability>") ;
              if(Error==null)  Error="Undefined error" ;

                             mPhones[i].LastError= Error ;
                        }
     else               {
                        }
   }

     if(mWaitReply==true && e.getInvokeId==mWaitID)  mWaitReply=false ;
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий мониторинга звонка 3-rd party                   */

  void  iOnCallClearedEvent(object sender, ThirdPartyCallController.CallClearedEventArgs e)
{
   int  i ;

    iLog("CALL - CallClearedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else               { 
                                               mPhones[i].CallStatus        ="CallCleared" ;
                                               mPhones[i].Events.CallCleared= true ;
                                               mPhones[i].ConGlobalId       = null ;
                                               mPhones[i].ConLocalId        = null ;
        if(mPhones[i].Times.ClearTime==null )  mPhones[i].Times.ClearTime   =DateTime.Now ;

        if(mPhones[i].CallMonitorId==null &&
           mPhones[i].ConGlobalId  ==null   )  mPhones[i].CallStatus        ="ReadyForNext" ;
                      } 
}

  void  iOnConferencedEvent(object sender, ThirdPartyCallController.ConferencedEventArgs e)
{
    iLog("CALL - ConferencedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnConnectionClearedEvent(object sender, ThirdPartyCallController.ConnectionClearedEventArgs e)
{
   int  i ;

    iLog("CALL - ConnectionClearedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)   iLog("WARNING! Unexpected call event!");
   else               {
                                                           mPhones[i].CallStatus              ="ConnectionCleared" ;
                                                           mPhones[i].Events.ConnectionCleared= true ;
        if(mPhones[i].Times.ClearTime==DateTime.MinValue)  mPhones[i].Times.ClearTime         =DateTime.Now ;
                      }
}

  void  iOnDeliveredEvent(object sender, ThirdPartyCallController.DeliveredEventEventArgs e)
{
   int  i ;

    iLog("CALL - DeliveredEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId  ==e.getMonitorId ||
                 mPhones[i].CallMonitorId_t==e.getMonitorId   )  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else              {
         if(mPhones[i].CallMonitorId==e.getMonitorId) {
                           mPhones[i].CallStatus        ="Delivered" ;
                           mPhones[i].Events.Delivered  =true ;
                           mPhones[i].Times.DeliveryTime=DateTime.Now ;
                                                      }
         else                                         {
                           mPhones[i].Events.Delivered_t=true ;
                                                      }
                     }
}

  void  iOnDivertedEvent(object sender, ThirdPartyCallController.DivertedEventEventArgs e)
{
    iLog("CALL - DivertedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnEstablishedEvent(object sender, ThirdPartyCallController.EstablishedEventArgs e)
{
   int  i ;

    iLog("CALL - EstablishedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId  ==e.getMonitorId ||
                 mPhones[i].CallMonitorId_t==e.getMonitorId   )  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else              {
         if(mPhones[i].CallMonitorId==e.getMonitorId) {
                                                                 mPhones[i].CallStatus        ="Established" ;
                                                                 mPhones[i].Events.Established= true ;
           if(mPhones[i].Times.ConnectTime ==DateTime.MinValue)  mPhones[i].Times.ConnectTime =DateTime.Now ;
           if(mPhones[i].Times.DeliveryTime==DateTime.MinValue)  mPhones[i].Times.DeliveryTime=DateTime.Now ;
                                                      }
         else                                         {
                                                          mPhones[i].Events.Established_t=true ;
                                                      }
                     }
}

  void  iOnFailedEvent(object sender, ThirdPartyCallController.FailedEventArgs e)
{
   int  i ;

    iLog("CALL - FailedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId  ==e.getMonitorId ||
                 mPhones[i].CallMonitorId_t==e.getMonitorId   )  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else              { 
         if(mPhones[i].CallMonitorId==e.getMonitorId) {
                          mPhones[i].CallStatus   ="Failed" ;
                          mPhones[i].Events.Failed= true ;
                                                      }
         else                                         {
                          mPhones[i].Events.Failed_t=true ;
                                                      }
                     }
}

  void  iOnHeldEvent(object sender, ThirdPartyCallController.HeldEventArgs e)
{
    iLog("CALL - HeldEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnNetworkReachedEvent(object sender, ThirdPartyCallController.NetworkReachedEventArgs e)
{
   int  i ;

    iLog("CALL - NetworkReachedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else              {
                        mPhones[i].CallStatus           ="NetworkReached" ;
                        mPhones[i].Events.NetworkReached= true ;
                     }
}

  void  iOnOriginatedEvent(object sender, ThirdPartyCallController.OriginatedEventArgs e)
{
    iLog("CALL - OriginatedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnQueuedEvent(object sender, ThirdPartyCallController.QueueEventArgs e)
{
    iLog("CALL - QueuedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnRetrievedEvent(object sender, ThirdPartyCallController.RetrievedEventArgs e)
{
    iLog("CALL - RetrievedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnServiceInitiatedEvent(object sender, ThirdPartyCallController.ServiceInitiatedEventArgs e)
{
    iLog("CALL - ServiceInitiatedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnTransferredEvent(object sender, ThirdPartyCallController.TransferredEventArgs e)
{
   int  i ;

    iLog("CALL - TransferredEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].CallMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected call event!");
   else              {
                        mPhones[i].CallStatus       ="Transfered" ;
                        mPhones[i].Events.Transfered= true ;
                     }
}
/*******************************************************************************/
/*                                                                             */
/*             Ловушки событий мониторинга Media-функций                       */

  void  iOnMediaStartedEvent(object sender, Media.MediaStartedEventArgs e)
{
    iLog("CALL - Media:MediaStartedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaStoppedEvent(object sender, Media.MediaStoppedEventArgs e)
{
    iLog("CALL - Media:MediaStoppedEvent " + e.getMonitorId);
}

  void  iOnMediaPlayingEvent(object sender, Media.PlayingEventArgs e)
{
    iLog("CALL - Media:PlayingEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaPlayingStoppedEvent(object sender, Media.PlayingStoppedEventArgs e)
{
    iLog("CALL - Media:PlayingStoppedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaPlayingSuspendedEvent(object sender, Media.PlayingSuspendedEventArgs e)
{
    iLog("CALL - Media:PlayingSuspendedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaRecordingEvent(object sender, Media.RecordingEventArgs e)
{
    iLog("CALL - Media:RecordingEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaRecordingStoppedEvent(object sender, Media.RecordingStoppedEventArgs e)
{
    iLog("CALL - Media:RecordingStoppedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaRecordingSuspendedEvent(object sender, Media.RecordingSuspendedEventArgs e)
{
    iLog("CALL - Media:RecordingSuspendedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaToneDetectedEvent(object sender, Media.ToneDetectedEventArgs e)
{
    iLog("CALL - Media:ToneDetectedEvent " + e.getMonitorId);

    iLogAndWait("\nUNHANDLED");
}

  void  iOnMediaTonesRetrievedEvent(object sender, Media.TonesRetrievedEventArgs e)
{
  int  i ;

    iLog("CALL - Media:TonesRetrievedEvent " + e.getMonitorId);

           for(i=0 ; i<mPHONES_MAX ; i++)
             if(mPhones[i]!=null)
              if(mPhones[i].MediaMonitorId==e.getMonitorId)  break ;

   if(i>=mPHONES_MAX)  iLog("WARNING! Unexpected media event!");
   else              { 
                          mPhones[i].Events.MediaTones       =e.getTones ;
                          mPhones[i].Events.MediaTonesFlushed=true ;
                     }
}

/*******************************************************************************/
/*                                                                             */
/*                 Вырезание фрагмента из строки                               */

  string iExtract(string text, string mark_1, string mark_2)
{
   int  idx_1 ;
   int  idx_2 ;


        idx_1=text.IndexOf(mark_1) ;
        idx_2=text.IndexOf(mark_2) ;

     if(idx_1==-1 || idx_2==-1)  return(null) ;

        idx_1+=mark_1.Length ;

   return( text.Substring(idx_1, idx_2-idx_1) ) ;
}
/*******************************************************************************/
/*                                                                             */
/*                                  Логирование                                */

  public virtual void iLog(string text)
{
    Console.WriteLine(text);
}

  public virtual void iLogAndWait(string text)
{
    Console.WriteLine(text);
//  Console.ReadLine();
}

  public virtual void iLogException(string text)
{
    Console.WriteLine(text);
}
/*******************************************************************************/
/*******************************************************************************/

// DMCC_service END
}
// DMCC_iface END
}
