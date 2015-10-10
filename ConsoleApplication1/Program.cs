using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using System.Diagnostics;
using DMCC_iface;

namespace ConsoleApplication1
{


    class Program
    {
/*******************************************************************************/
/*                                                                             */
/*                      Управляющие переменные                                 */


     static string  Regime ;          // Режим запуска программы: Test - тестирование
                                      //                          Scan - сканирование диапазона номеров
                                      //                          Call - соединение с номерами, поступающими в режиме реального времени
                                      //                          Play - обзвон списка номеров с обратной связью
                                      //                          Kick - сканирование диапазона номеров на "касание"
                                      //                          Flag - работа с флажной системой контроля
     static string  CfgPath ;         // Путь к конфигурационному файлу

     static string  Version ;

/*----------------------------------------- Общее управление рабочим процессом */

     static string  NULL ;

/*----------------------------------------- Общее управление рабочим процессом */

     static    int  ClearFlag ;            /* Флаг полной инициализации контекста */

     static    int  AgentPeriod ;          /* Период работы исполнительного агента */
     static    int  GeneratePeriod ;       /* Период генерации номеров */
     static    int  GroupsGeneratePeriod ; /* Период генерации групп номеров */

     static    int  ActiveTime ;           /* Время работы программы, минут */

/*------------------------------------ Параметры подсоединения к серверу Avaya */

     static string  Avaya_ServiceIP    ; 
     static    int  Avaya_ServicePort  ;
     static string  Avaya_UserName     ; 
     static string  Avaya_UserPassword ; 
     static string  Avaya_SwitchName ; 
     static string  Avaya_SwitchIP ; 

/*------------------------------------------------------------ Станции дозвона */

    public class Quota
    {
       public    int  Calls_Crn_Trip ;      /* Число звонков за рейс */
       public    int  Times_Crn_Trip ;      /* Времени звонков за рейс, секунд */
       public    int  Calls_Crn_Total ;     /* Общее число звонков */
       public    int  Times_Crn_Total ;     /* Общее время звонков, секунд */
       public    int  Calls_Max_Trip ;      /* Квота звонков на рейс */
       public    int  Times_Max_Trip ;      /* Квота времени на рейс, секунд */
       public    int  Calls_Max_Total ;     /* Общая квота звонков */
       public    int  Times_Max_Total ;     /* Общая квота времени, секунд */
       public string  Comment ;             /* Идентификатор группы или примечание */
       public string  Row ;                 /* Строковые данные */
       public   bool  processed ;           /* Признак обработки */
    }

    public class Station
    {
           public             string  Extension ;       /* Номер станции */
           public             string  Password ;        /* Пароль */
           public                int  Phone ;           /* Индекс телефона */
           public             string  Target ;          /* Номер целевого телефона */
           public             string  QueueId ;         /* Идентификатор очереди */
           public             string  Link ;            /* Индентификатор соединения */

           public             string  CallStatus ;      /* Системный статус соединения */
           public  DMCC_phone_events  Events ;          /* Флаги событий соединения */
           public             string  Status ;          /* Статус соединения */
           public             string  Error ;           /* Текст ошибки */
           public                int  Delivery_time ;   /* Время от начала звонка до входящего сигнала */
           public                int  Connect_time ;    /* Время от начала звонка до соединения */
           public                int  Active_time ;     /* Время активного соединения */
           public                int  Active_drop ;     /* Время максимального удержания активного соединения */
           public                int  Completed ;       /* Флаг завершения: "Все номера обработаны"=1, "Ошибка звонка"=2, "Выбрана квота"=3 */
           public             string  Complete_reason ; /* Уточнение причины завершения */
           public                int  Idx ;             /* Внутренний индекс */

           public           DateTime  NextAttempt ;     /* Время следующей попытки соединения */
           public              Quota  Quota ;           /* Квота по звонкам */

        public Station()
        {
                Phone= -1 ;
               Target=null ;
              QueueId=null ;
                Error=null ;
            Completed=  0 ;
                Quota=new Quota() ;
        }  
    }

    static Station[]  Stations ;
    static    string  Stations_list ;

    public const int  ERROR_PAUSE=300 ;  
    public const int  HANGUP_PAUSE=300 ;  

    public const int  QUOTAS_MAX=1000 ;
    static   Quota[]  Quotas ;
    static       int  Quotas_cnt ;

    static   Quota[]  Groups ;
    static       int  Groups_cnt ;

/*---------------------------------------------------------- Номера назначения */

    public const int  TARGETS_MAX=100000 ;

    public class Target
    {
        public   string  Phone ;       /* Номер телефона */
        public   string  Group ;       /* Идентификатор группы */
        public   string  ReCallSpec ;  /* Формула повторных звонков */
        public   string  QueueId ;     /* Идентификатор в очереди */
        public DateTime  NextAttempt ; /* Время следующей попытки соединения */
        public      int  use_cnt ;     /* Счетчик возможного использования */
        public      int  use_init ;    /* Счетчик возможного использования, начальное значение */

        public Target()
        {
                 Phone=null ;
                 Group=null ;
              use_cnt =  0 ;
              use_init=  0 ;
        }  
    }

    public const int  TARGETS_AG_MAX=100 ;

    public class TargetGroup
    {
        public   string  Station ;  /* Станция */
        public      int  Size ;     /* Количество номеров в группе */
        public   string  ScanType ; /* Тип диапазона номеров */
        public   string  Targets ;  /* Диапазон номеров */
        public   string  List ;     /* Текущий список номеров группы */
        public     bool  done ;     /* Метка отработки */

        public TargetGroup()
        {
               Station=null ;
                  Size=  0 ;
              ScanType=null ;
               Targets=null ;
                  List=null ;
                  done=false ;
        }
    }

    public class TargetAllias
    {
        public   string  Station ;  /* Станция */
        public   string  Group ;    /* Группа */
        public      int  Size ;     /* Количество номеров в группе */
        public   string  Prefix ;   /* Префикс алиаса */
        public      int  cnt ;      /* Рабочий счетчик */
        public     bool  done ;     /* Метка отработки */

        public TargetAllias()
        {
               Station=null ;
                 Group=null ;
                  Size=  0 ;
                Prefix=null ;
                   cnt=  0 ;
                  done=false ;
        }
    }

     static       Target[]  Targets ;
     static            int  Targets_cnt ;
     static            int  Targets_new ;
     static         string  TargetsPath ;

     static  TargetGroup[]  TargetsGroups ;
     static            int  TargetsGroups_cnt ;
     static         string  TargetsGroups_list ;

     static TargetAllias[]  TargetsAllias ;
     static            int  TargetsAllias_cnt ;
     static         string  TargetsAllias_list ;

     static         string  ScanType  ;            /* Режим сканирования: Single, Heap */
     static         string  ScanPrefix ;           /* Набор фиксированных подстановок сканирования */
     static       string[]  ScanPrefixList ;
     static         string  ScanNumbers ;          /* Список диапазонов дозвона через запятую или путь к файлу со списком (наюинается с символа @) */
     static            int  ScanCompleted ;
     static            int  ScanIndexMin ;
     static            int  ScanIndexMax ;

     static         string  CallsFolder ;          /* Путь к папке запросов на вызов */
     static         string  ControlFolder ;        /* Путь к папке флаг-файлов обработки вызовов */
     static         string  ReCallSpec  ;          /* Формула повторных звонков */

     static         string  TalkFile  ;            /* Файл аудио-приветствия */

/*----------------------------------------------- Диапазоны допустимых номеров */

    public const int  RANGES_MAX=1000 ;

    public class Range
    {
        public   string  PhoneMin ;
        public   string  PhoneMax ;
        public   string  Row ;                 /* Строковые данные */

        public Range()
        {
              PhoneMin="" ;
              PhoneMax="" ;
        }  
    }

     static  Range[]  Ranges ;
     static      int  Ranges_cnt ;
     static   string  RangesPath ;
     static   string  RangesPrefix ;

/*------------------------------------------------ Действия по тоновому набору */

    public const int  TONES_ACTIONS_MAX=100 ;

    public class TonesAction
    {
        public   string  tones ;       /* Тоновая строка */
        public   string  action ;      /* Действие: call, write, execute */
        public   string  target ;      /* Объект действия */
        public      int  use_cnt ;     /* Счетчик использования */
        public      int  use_max ;     /* Ограничитель счетчика использования */

        public TonesAction()
        {
                 tones=null ;
                action=null ;
                target=null ;
               use_cnt=  0 ;
               use_max=  0 ;
        }  
    }

     static TonesAction[]  TonesActions ;
     static           int  TonesActions_cnt ;

/*--------------------------------------------------- Слежение за флаг-файлами */

    public const int  FLAGFILE_ACTIONS_MAX=1000 ;

    public class FlagFileAction
    {
        public      string    id ;                /* Идентификатор правила */
        public      string    check_path ;        /* Путь к контрольному файлу */
        public      string    check_type ;        /* Тип контроля: existence, absence, change */
        public         int    check_period ;      /* Периодичность контроля, секунд */
        public         int    check_threshold ;   /* Порог срабатывания, секунд */
        public      string    targets ;           /* Перечень номеров дозвона */
        public      string    talk_file ;         /* Путь к проигрываемому тексту */
        public      string    alert_type ;        /* Тип дозвона: any, all */
        public         int    alert_attempts ;    /* Число попыток дозвона */
        public TonesAction[]  actions ;           /* Действия по кнопкам */

        public    DateTime    next_check ;        /* Время следующей проверки */
        public    DateTime    event_mark ;        /* Время срабатывания события при задании задержки check_threshold */
        public         int    event_done ;        /* Флаг отработки события */

        public FlagFileAction()
        {
           check_path     =null ;
           check_type     =null ;
           check_period   =  0 ;
           check_threshold=  0 ;
           targets        =null ;
           talk_file      =null ;
           alert_type     =null ;
           alert_attempts =  0 ;
           actions        =new TonesAction[20] ;

           next_check     =DateTime.MinValue ;
           event_mark     =DateTime.MinValue ;
           event_done     =  0 ;
        }
    }

    public class TargetAction
    {
        public      string    id ;                /* Идентификатор правила */
        public      string    linked_type ;       /* Правило оповещения =FlagFileAction.alert_type */
        public      string    phone ;             /* Номер телефона */
        public      string    station ;           /* Назначенная станция */
        public        bool    exclude ;
        public         int    attempts ;
        public         int    priority ;

        public TargetAction()
        {
           id      =null ;
           phone   =null ;
           station =null ;
           exclude =false ;
           attempts= 0 ;
           priority= 0 ;
        }
    }


     static           string  FlagsSpecPath ;        /* Путь к файлу спецификации отслеживаемых флаг-файлов */

     static FlagFileAction[]  FlagFileActions ;      /* Список отслеживаемых файлов */
     static              int  FlagFileActions_cnt ;

     static   TargetAction[]  TargetActions ;        /* Список звонков */
     static              int  TargetActions_cnt ;

/*---------------------------------------------------------- Календарь режимов */

    public class Calendar
    {
       public   bool  Disable ;             /* Метка отключения */
       public string  Date ;                /* Дата */
       public    int  WeekDay ;             /* День недели: 1-понедельник, ..., 7-воскресенье */
       public   bool  AnyDay ;              /* Все дни */
       public string  TimeStart ;           /* Время начала звонков */
       public string  TimeStop ;            /* Время окончания звонков */
       public    int  CallsPerHour ;        /* Число звонков в час */
       public string  Row ;                 /* Строковые данные */
    }

    public  const int  DAYS_MAX=1000 ;
    static Calendar[]  Days ;
    static        int  Days_cnt ;

        static string  CalendarPath ;         /* Путь к файлу календаря */
        static    int  CalendarCycle ;        /* Периодичность опроса файла календаря, минут */

/*-------------------------------------------------------- Управление дозвоном */

       static    int  Simulation ;           /* Флаг эмуляции дозвона */

       static    int  DropDelivery ;         /* Максимальное время ожидания входящего звонка, секунд */
       static    int  DropConnect ;          /* Максимальное время ожидания соединения, секунд */
       static    int  DropActive ;           /* Максимальное время удержания соединения, секунд */
       static    int  RobotConnect ;         /* Определение "Робота" - максимальное время ожидания соединения, секунд */
       static    int  RobotActive ;          /* Определение "Робота" - минимальное время удержания соединения, секунд */
       static string  RandomActive ;         /* Закон распределения максимального время удержания соединения:Fixed,Uniform,Top15 */
       static double  PulseActive ;          /* Скважность нагрузки */

       static Random  Rand ;                 /* Генератор случайных чисел */
       static Random  RandTarget ;           /* Генератор случайных чисел для номеров */

       static string  QuotaPath ;            /* Путь к файлу квот */
       static    int  QuotaCycle ;           /* Периодичность обновления файла квот, минут */

/*---------------------------------------------------- Управление логированием */
                  
       static string  StatisticsPath ;       /* Путь к файлу общей статистики */
       static string  StatisticsHeader ;     /* Строка разделителя заголовка для файла общей статистики */
       static string  ScanRobotsPath ;       /* Путь к файлу кандидатов в "Роботы" */

       static string  ResultsPath ;          /* Путь к файлу результатов */

       static    int  WaitUser ;             /* Флаг ожидания нажатия клавиш */
       static    int  WaitUserStrong ;       /* Флаг обязательного ожидания нажатия клавиш */
       static    int  TraceOnly ;            /* Флаг вывода только сообщений трассировки */

/*------------------------------------------------ Управление очередью событий */

       static   long  Queue_Seq ;            /* Номер события */


/*******************************************************************************/
/*                                                                             */
/*                                  Main                                       */

static void Main(string[] args)

{
     int  status ;

/*-------------------------------------------------------------- Инициализация */

                Version="28.06.2015" ;

/*------------------------------ Разбор и контроль аргументов командной строки */

                                WaitUserStrong=1 ;

   if(args.Count()<2) {
                          MessageWait("ERROR - invalid number of arguments\r\n\r\n" +
                                      "  Arguments list must be:\r\n" +
                                      "       Scan <configuration file path> - to run Scan mode\r\n" +
                                      "       Kick <configuration file path> - to run Kick mode\r\n" +
                                      "       Play <configuration file path> - to run Play mode\r\n" +
                                      "       Call <configuration file path> - to run Call mode\r\n" +
                                      "       Flag <configuration file path> - to run Flag mode\r\n" +
                                      "       Test <configuration file path> - to run Test mode\r\n" +
                                      "       Clear                          - to run clear work context\r\n" +
                                      "      Quota <quota file path>         - to create quota template\r\n\r\n" +
                                      "   Calendar <calendar file path>      - to create calendar template\r\n\r\n" +
                                      "  To create configuration template specify non-existent file\r\n") ;
                            return ;
                      }

        Regime=args[0] ;
       CfgPath=args[1] ;

   if( String.Compare(Regime, "Test",     true)!=0 && 
       String.Compare(Regime, "Scan",     true)!=0 &&
       String.Compare(Regime, "Kick",     true)!=0 &&
       String.Compare(Regime, "Play",     true)!=0 &&
       String.Compare(Regime, "Call",     true)!=0 &&
       String.Compare(Regime, "Flag",     true)!=0 &&
       String.Compare(Regime, "Clear",    true)!=0 &&
       String.Compare(Regime, "Quota",    true)!=0 &&
       String.Compare(Regime, "Calendar", true)!=0   ) {
                          MessageWait("ERROR - unknown regime specified\r\n" +
                                      "  Regime can be: Scan, Kick, Play, Call, Test, Quota, Calendar\r\n") ;
                            return ;
                                                       }

   if(String.Compare(Regime, "Scan", true)==0 &&
                              args.Count()==3   )  TraceOnly=1 ;

   if(String.Compare(Regime, "Kick", true)==0 &&
                              args.Count()==3   )  TraceOnly=1 ;

   if(String.Compare(Regime, "Call", true)==0 &&
                              args.Count()==4   )  TraceOnly=1 ;

   if(String.Compare(Regime, "Play", true)==0 &&
                              args.Count()==4   )  TraceOnly=1 ;

   if(String.Compare(Regime, "Flag", true)==0 &&
                              args.Count()==4   )  TraceOnly=1 ;

/*--------------------------------------- Создание шаблонов управляющих файлов */

   if(String.Compare(Regime, "Quota", true)==0) {

               status=QuotaTemplate(CfgPath) ;
            if(status==0)  MessageWait("Quota template created\r\n") ;
                                 return ;
                                                }

   if(String.Compare(Regime, "Calendar", true)==0) {

               status=CalendarTemplate(CfgPath) ;
            if(status==0)  MessageWait("Calendar template created\r\n") ;
                                 return ;
                                                   }
/*--------------------------------------------------- Выделение общих ресурсов */

       FlagFileActions    =new FlagFileAction[FLAGFILE_ACTIONS_MAX] ;
       FlagFileActions_cnt= 0 ;

         TargetActions    =new TargetAction[FLAGFILE_ACTIONS_MAX] ;
         TargetActions_cnt= 0 ;

          TonesActions    =new TonesAction[TONES_ACTIONS_MAX] ;
          TonesActions_cnt= 0 ;

/*----------------------------------------- Считывание конфигурационного файла */

       Message("\r\nProgram version " + Version + "\r\n\r\n") ;

   if(!File.Exists(CfgPath)) {

               status=ConfigTemplate(Regime, CfgPath) ;
            if(status==0)  MessageWait("Configuration template created\r\n") ;
                                 return ;
                             }

      status=ReadConfig(CfgPath) ;
   if(status!=0)  return ;

  if(String.Compare(Regime, "Flag", true)==0) {
       status=ReadFileFlagSpecification() ;
   if(status!=0)  return ;
                                              }

      status=QuotaFileCheck(true) ;
   if(status!=0)  return ;

      status=CalendarFileCheck() ;
   if(status!=0)  return ;

      status=RangesFileCheck() ;
   if(status!=0)  return ;

              Rand=new Random() ;

               Log.Initialize() ;

                                WaitUserStrong=0 ;

/*---------------------------------------------- Разводка по ветвям управления */

/*------------------------------------------------- Очистка рабочего контекста */

   if(String.Compare(Regime, "Clear", true)==0) {
                                                  Message("CLEAR mode\r\n") ;
                                                        ClearFlag=1 ;
                                                    InitControl() ;
                                                }

   if(String.Compare(Regime, "Test", true)==0)  Test() ;

   if(String.Compare(Regime, "Scan", true)==0)
    if(args.Count()==2)                         Scan() ;
    else                                  SingleScan() ;

   if(String.Compare(Regime, "Kick", true)==0)
    if(args.Count()==2)                         Kick() ;
    else                                  SingleKick() ;

   if(String.Compare(Regime, "Call", true)==0)
    if(args.Count()==2)                         Call() ;
    else                                  SingleCall(args[2], args[3]) ;

   if(String.Compare(Regime, "Play", true)==0)
    if(args.Count()==2)                         Play() ;
    else                                  SinglePlay(args[2], args[3]) ;

   if(String.Compare(Regime, "Flag", true)==0)
    if(args.Count()==2)                         Flag() ;
    else                                  SinglePlay(args[2], args[3]) ;

/*---------------------------------------------------------- Завершение работы */

//            WaitUserStrong=1 ;

       MessageWait("\r\nProgram complete\r\n") ;

/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                 Ветвь обзвона диапазона номеров                             */

static void Scan()

{
          DMCC_this  iface ;
            Boolean  error ;
           DateTime  time ;
            Process  proc ;
            Boolean  start_agent ;
            Boolean  calls_over ;         /* Флаг полного перебора заданного набора номеров */
                int  status ;
                int  quota ;
                int  count ;
     ConsoleKeyInfo  chr ;
           DateTime  agent_time ;
           DateTime  generate_time ;
           DateTime  groups_time ;
           DateTime  complete_time ;
           DateTime  quota_time ;
           DateTime  calendar_time ;
           string[]  calls ;
             string  target ;
             string  station ;
                int  grp ;
                int  n ;
                int  i ;

/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations     ==null) {
                              MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                                return ;
                            }
   if(TargetsGroups_cnt==0)
    if(ScanType     ==null) {
                              MessageWait("ERROR Configuration - <ScanType> specificator is missed") ;
                                return ;
                            }
   if(TargetsGroups_cnt==0)
    if(ScanNumbers  ==null) {
                              MessageWait("ERROR Configuration - <ScanNumbers> specificator is missed") ;
                                return ;
                            }
    if(ControlFolder==null) {
                              MessageWait("ERROR Configuration - <ControlFolder> specificator is missed") ;
                                return ;
                            }
    if(DropDelivery ==   0) {
                              MessageWait("ERROR Configuration - <DropDelivery> specificator is missed") ;
                                return ;
                            }
    if(DropConnect  ==   0) {
                              MessageWait("ERROR Configuration - <DropConnect> specificator is missed") ;
                                return ;
                            }
    if(DropActive   ==   0) {
                              MessageWait("ERROR Configuration - <DropActive> specificator is missed") ;
                                return ;
                            }
    if(RobotConnect ==   0) {
                              MessageWait("ERROR Configuration - <RobotConnect> specificator is missed") ;
                                return ;
                            }
    if(RobotActive  ==   0) {
                              MessageWait("ERROR Configuration - <RobotConnect> specificator is missed") ;
                                return ;
                            }

    if(PulseActive!=0)
    if(PulseActive  <  0.1 ||
       PulseActive  >  0.9   ) {
                              MessageWait("ERROR Configuration - <PulseActive> mast be in the range from 0.1 to 0.9") ;
                                return ;
                               }

   if(TargetsGroups_cnt==0)
    if(ScanType     !="Single" && 
       ScanType     !="Heap"     ) {
                             MessageWait("ERROR Configuration - <ScanType> mast be 'Single' or 'Heap'") ;
                               return ;
                                   }

    if(RandomActive!=null      &&
       RandomActive!="Fixed"   && 
       RandomActive!="Uniform" && 
       RandomActive!="Top15"     ) {
                             MessageWait("ERROR Configuration - <RandomActive> mast be empty, 'Fixed', 'Uniform' or 'Top15'") ;
                               return ;
                                   }
/*----------------------------------------------------------------- Подготовка */
 
                             Message("SCAN mode\r\n") ;
          if(Simulation==1)  Message("SIMULATION mode\r\n") ;

       status=WriteScanStatistics(null) ;                                     /* Проверка записи общей статистики */
    if(status<0)  return ;

    if(ActiveTime>0)  complete_time=DateTime.Now.AddMinutes(ActiveTime) ;     /* Определяем момент окончания работы */
    else              complete_time=DateTime.Now.AddMinutes(24*60) ;

                         quota_time=DateTime.Now.AddMinutes(QuotaCycle) ;     /* Определяем время первого обновления файла квот */

/*------------------------------------ Проверка создания виртуальных телефонов */

                             error=false ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Соединение с сервером */
             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Scan" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }

                          }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

                         Message("Create Phones...") ;

        foreach(Station phone in Stations) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                                  break ;
                            }
                                           }

                         Message("Delete Phones...") ;

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {

        foreach(Station phone in Stations)  phone.Phone=phone.Idx ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Отключение от сервера */
              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }

          if(error)  return ; 

               MessageWait("SUCCESS Create Phones\r\n") ;

/*------------------------------------------- Формирование списка сканирования */

               Targets          =new Target[TARGETS_MAX] ;
               Targets_cnt      = 0 ;

      status=FormTargetsByFile(ControlFolder+"\\targets.save", false) ;       /* Восстанавливаем сохраненный список */
   if(status!=0)                                                              /* Если его нет                              */
    if(TargetsGroups_cnt==0) {                                                /*  и это не групповой режим - создаем новый */

         ScanNumbers=ScanNumbers.Trim(',') ;

      if(ScanNumbers.Substring(0,1)=="@")  status=FormTargetsByFile  (ScanNumbers.Substring(1), true) ;
      else                                 status=FormTargetsByRanges(ScanNumbers) ;

      if(status<0)  return ;

                                 ResetNextTarget(null) ;                       /* Инициализируем диапазон сканирования */
                             }
/*---------------------------------------------------------- Инициализация квот */

     foreach(Station phone in Stations) {  phone.Quota.Calls_Crn_Trip=0 ;
                                           phone.Quota.Times_Crn_Trip=0 ;  }

     for(i=0 ; i<Groups_cnt ; i++) {  Groups[i].Calls_Crn_Trip=0 ;
                                      Groups[i].Times_Crn_Trip=0 ;  }

                 QuotaFileCheck(false) ;

/*---------------------------------------------------------- Цикл сканирования */

                            InitControl() ;                                   /* Инициализируем параметры управления агентом */

                ScanCompleted= 0 ;

                calendar_time=DateTime.Now.AddMinutes(CalendarCycle) ;        /* Определяем время первого обновления файла календаря */
                   agent_time=DateTime.Now  ;
                generate_time=DateTime.Now  ;
                  groups_time=DateTime.Now  ;
                   calls_over=false ;
                       quota = 0 ;

     do {
                  Thread.Sleep(1000) ;

                    time=DateTime.Now ;

/*------------------------------------------------ Обработка консольных команд */

       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                       AddControl("Urgent", "Stop") ;
                                            ScanCompleted=1 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        
                                             ) ; 
                                     }
                                   } 

           if(ScanCompleted!=0)  break ;

/*------------------------------------------------- Контроль завершения работы */

/*- - - - - - - - - - - - - - - - - - - - -  Проверка по общему времени работы */
     if(ActiveTime>0)                                                         /* Если задано время работы и оно вышло - */
      if(DateTime.Now>complete_time) {                                        /*   - завершаем работу                   */  
                                       AddControl("Urgent", "Stop") ;
                                          Message("Active time is over\r\n") ;
                                                     break ;
                                     }
/*-------------------------------------------------------------- Контроль квот */

        foreach(Station phone in Stations) {

            if(phone.Completed!=0)  break ;
/*- - - - - - - - - - - - - - - - - - - - - -  Проверка по персональным квотам */
            if(phone.Quota.Calls_Max_Trip >   0                        &&
               phone.Quota.Calls_Max_Trip <=phone.Quota.Calls_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Trip Calls" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(phone.Quota.Calls_Max_Total>   0                        &&
               phone.Quota.Calls_Max_Total<=phone.Quota.Calls_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Total Calls" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(phone.Quota.Times_Max_Trip >   0                        &&
               phone.Quota.Times_Max_Trip <=phone.Quota.Times_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Trip Time" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(phone.Quota.Times_Max_Total>   0                        &&
               phone.Quota.Times_Max_Total<=phone.Quota.Times_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Total Time" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
/*- - - - - - - - - - - - - - - - - - - - - - - - Проверка по групповым квотам */
                            grp=GetQuotaGroup(phone.Quota) ;

           if(grp>=0) {

            if(Groups[grp].Calls_Max_Trip >   0                        &&
               Groups[grp].Calls_Max_Trip <=Groups[grp].Calls_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Trip Calls" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(Groups[grp].Calls_Max_Total>   0                        &&
               Groups[grp].Calls_Max_Total<=Groups[grp].Calls_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Total Calls" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(Groups[grp].Times_Max_Trip >   0                        &&
               Groups[grp].Times_Max_Trip <=Groups[grp].Times_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Trip Time" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
            if(Groups[grp].Times_Max_Total>   0                        &&
               Groups[grp].Times_Max_Total<=Groups[grp].Times_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Total Time" ;
                                Message(phone.Complete_reason) ;
                                                             continue ;
                                                                           }
                      }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                           } 

                                           ScanCompleted=1 ;

        foreach(Station phone in Stations) 
                   if(phone.Completed==0)  ScanCompleted=0 ;

           if(ScanCompleted!=0)  break ;

/*------------------------------------------------- Обновление файла календаря */

        if(CalendarCycle>0)
         if(calendar_time<DateTime.Now) {
                                           CalendarFileCheck() ;
                     calendar_time=calendar_time.AddMinutes(CalendarCycle) ;
                                        }
/*------------------------------------------ Управление режимом работы агентов */

                                            start_agent=false ;

        if(time>=agent_time) {                                                 /* Квантование периодов запуска Агента */
                              agent_time=agent_time.AddSeconds(AgentPeriod) ;
                                            start_agent=true ;
                             }
/*---------------------------------------------------- Генерация групп номеров */

       if(TargetsGroups_cnt>0)
        if(time>=groups_time) {                                               /* Квантуем периоды генерации номеров */  

                      groups_time=groups_time.AddMinutes(GroupsGeneratePeriod) ;

                                    FormTargetsGroups() ;
                              }
/*---------------------------------------------------------- Генерация номеров */

        if(time>=generate_time) {                                              /* Квантуем периоды генерации номеров */  

                      generate_time=generate_time.AddSeconds(GeneratePeriod) ;

             quota=CalendarQuota(time) ;                                      /* Определение режима периода */ 
          if(quota <0)  quota=6000 ;

             quota/=3600/GeneratePeriod ;                                     /* Переситываем квоту на период генерации */

          if(quota>0)                                                         /* Если активный режим... */
           if(generate_time<=agent_time) {                                    /* Звонки генерятся кроме последнего периода */
/*- - - - - - - - - - - - - - - - - - - - - - - - - Формирование групп номеров */
            if(TargetsGroups_cnt>0) {

              for(n=1 ; n<=TargetsAllias_cnt ; n++)  TargetsAllias[n].done=false ;

              for(n=1 ; n<=TargetsAllias_cnt ; n++) 
                if(TargetsAllias[n].done==false) {

                  station=TargetsAllias[n].Station ;

               for(count=0, i=n ; i<=TargetsAllias_cnt ; i++)
                 if(TargetsAllias[i].Station==station) {
                                 count+=TargetsAllias[i].Size ;
                                        TargetsAllias[i].done=true ;
                                                       }

                    calls=Directory.GetFiles(ControlFolder,                   /* Определяем число звонков "в стеке" */
                                                   "*."+station+".call") ;
                    count=count-calls.Count() ;                               /* Определяем, сколько звонков надо сгенерить */

               if(count>0) {
                                Message("Generate "+count+" calls for station "+station) ;

                 for( ; count>0 ; count--) {
                                Message("Generate number "+count) ;

                     target=GetNextTarget(station) ;                          /* Запрашиваем следующий номер */
                  if(target==null) {                                          /* Если все номера перебраны... */
                                      ResetNextTarget(station) ;              /*  ... перебираем номера с начала, */
                                Message("Reset calls for station "+station) ;
                                   }
                  else             {
                                       AddControl("Queue", target+"."+station) ;
                                   }
                                           }
                           }
               else        {
                               Message("No calls generated for group "+station) ;
                           }
                                                 }

                      SaveTargetsToFile(ControlFolder+"\\targets.save") ;     /* Сохранение состояния целевого пула */

                                    }
/*- - - - - - - - - - - - - - - - - - - - - Формирование едного потока номеров */
            else                    {
                    calls=Directory.GetFiles(ControlFolder, "*.call") ;       /* Определяем число звонков "в стеке" */
                    count=quota-calls.Count() ;                               /* Определяем, сколько звонков надо сгенерить */

               if(calls.Count()==0 && calls_over) {                           /* Если мы ожидали завершения перебора номеров... */
                                          Message("Numbers list is over") ;
                                                    ScanCompleted=1 ;
                                                        break ;
                                                  }

               if(count>0) {
                                Message("Generate "+count+" calls") ;

                 for( ; count>0 ; count--) {

                     target=GetNextTarget(null) ;                             /* Запрашиваем следующий номер */
                  if(target==null) {                                          /* Если все номера перебраны... */
                                     if(ActiveTime>0)  ResetNextTarget(null); /*  Если задано время активной работы - перебираем номера с начала, */
                                     else            {                        /*    если не задано - прекращаем генерацию номеров                 */
                                                          calls_over=true ;
                                                             break ;
                                                     }
                                   }
                  else             {
                                       AddControl("Queue", target) ;
                                   }
                                           }

                      SaveTargetsToFile(ControlFolder+"\\targets.save") ;     /* Сохранение состояния целевого пула */
                           }
               else        {
                               Message("No calls generated") ;
                           }
                                        }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                         }
                                }
/*--------------------------------------------------- Запуск агента исполнения */

       if(ScanCompleted==0) 
        if(start_agent) {
                            Message("Start AGENT for "+AgentPeriod+" seconds") ;

               proc                          =new Process() ;
               proc.StartInfo.FileName       =Environment.CommandLine.Substring(0, Environment.CommandLine.IndexOf(' '));
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Trim('\"');
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Replace(".vshost", "");
               proc.StartInfo.Arguments      ="Scan "+CfgPath+" Agent" ;
               proc.StartInfo.UseShellExecute= false ;

          try 
          {
               proc.Start() ;
          }
          catch (Exception exc)
          {
              Message("Calls processor start error: "+exc.Message) ;
          }

               proc=null ;

                        }
/*---------------------------------------------------------- Цикл сканирования */

        if(QuotaCycle>0)                                                      /* Обновление файла квот */
         if(quota_time<DateTime.Now) {
                                             QuotaFileCheck(true) ;
                         quota_time=quota_time.AddMinutes(QuotaCycle) ;       /* Определяем время следующего обновления файла квот */
                                     }

        } while(ScanCompleted==0) ;

/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");

}

/*******************************************************************************/
/*                                                                             */
/*                               Агент SCAN-обзвона                            */

static void SingleScan()

{
          DMCC_this  iface ;
   DMCC_phone_times  times ;
           DateTime  current_time ;
            Boolean  error ;
                int  status ;
           DateTime  complete_time ;
           DateTime  abort_time ;
           DateTime  quota_time ;
             string  action ;
                int  grp ;
                int  active_time ;

/*----------------------------------------------------------------- Подготовка */

                              times=new DMCC_phone_times() ;

                        Targets    =new Target[TARGETS_MAX] ;
                        Targets_cnt= 0 ;

                          TraceOnly= 0 ;

                        Log.MaxSize= 0 ;
  
                      complete_time=DateTime.Now.AddSeconds(AgentPeriod-60) ;
                         abort_time=complete_time.AddSeconds(30) ;
                         quota_time=DateTime.Now.AddSeconds(QuotaCycle) ;     /* Определяем время первого обновления файла квот */

/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="SingleScan" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("SingleScan - Connecting...") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           Message("SingleScan - ERROR - Connect:\r\n " + iface.Error + "\r\n") ;
                              return ;
                        }

                          }
/*------------------------------------------------------- Главный рабочий цикл */

   do {                                                                       /* BLOCK MAIN */

/*--------------------------------------------- Создание виртуальных телефонов */

                         Message("Create Phones...") ;

                                  error=true ;

        foreach(Station phone in Stations) {

          if(Simulation==1) {
                                phone.Phone=phone.Idx ;
                                      error=false ;
                                       continue ;
                            }  

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               phone.Error="Phone creation error" ;
                            }
          else              {
                                  error=false ;
                            }
                                           }

          if(error)  break ; 

                           MessageWait("SUCCESS Create Phones\r\n") ;

/*---------------------------------------------------------- Цикл сканирования */

     do {
                       Thread.Sleep(100) ;

        foreach(Station phone in Stations) {

/*------------------------------------------------ Обработка консольных команд */
#if REMARK
       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                            ScanCompleted=3 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        
                                             ) ; 
                                     }
                                   } 
#endif
/*---------------------------------------------- Обработка экстренных сигналов */

            action=GetNextControl(false, null, ref NULL) ;                    /* Запрашиваем налиюие сигналов */
         if(action=="Stop") {
                                ScanCompleted=3 ;
                            }

         if(ScanCompleted==3) {
                                        Message(phone.Phone + " stopped by user") ;

                                                phone.Status="UserBreaking" ;
                                                phone.Completed=3 ;
                                 iface.DropCall(phone.Phone) ;
                              }
/*------------------------------------------ Обработка ошибок инициации звонка */

         if(phone.Completed==2) {
           if(phone.NextAttempt<DateTime.Now)  phone.Completed=0 ;
                                }
/*----------------------------------------------------------- Инициация звонка */

        if(phone.Completed==0)
         if(phone.NextAttempt<DateTime.Now)
          if(phone.Target==null) do {
/*- - - - - - - - - - - - - - - - - - - - - - - -  Контроль времени завершения */
                if(complete_time<DateTime.Now) {                              /* Если время работы вышло - "закрываем" сканирование */
                                   Message("ACTIVE TIME completed") ;
                                                 phone.Completed=1 ;
                                                    break ;
                                               }
/*- - - - - - - - - - - - - - - - - - - - - -  Проверка по персональным квотам */
            if(phone.Quota.Calls_Max_Trip >   0                        &&
               phone.Quota.Calls_Max_Trip <=phone.Quota.Calls_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Trip Calls" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(phone.Quota.Calls_Max_Total>   0                        &&
               phone.Quota.Calls_Max_Total<=phone.Quota.Calls_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Total Calls" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(phone.Quota.Times_Max_Trip >   0                        &&
               phone.Quota.Times_Max_Trip <=phone.Quota.Times_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Trip Time" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(phone.Quota.Times_Max_Total>   0                        &&
               phone.Quota.Times_Max_Total<=phone.Quota.Times_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Quota Total Time" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
/*- - - - - - - - - - - - - - - - - - - - - - - - Проверка по групповым квотам */
                            grp=GetQuotaGroup(phone.Quota) ;

           if(grp>=0) {

            if(Groups[grp].Calls_Max_Trip >   0                        &&
               Groups[grp].Calls_Max_Trip <=Groups[grp].Calls_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Trip Calls" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(Groups[grp].Calls_Max_Total>   0                        &&
               Groups[grp].Calls_Max_Total<=Groups[grp].Calls_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Total Calls" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(Groups[grp].Times_Max_Trip >   0                        &&
               Groups[grp].Times_Max_Trip <=Groups[grp].Times_Crn_Trip   ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Trip Time" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
            if(Groups[grp].Times_Max_Total>   0                        &&
               Groups[grp].Times_Max_Total<=Groups[grp].Times_Crn_Total  ) {
                                        phone.Completed      =  3 ;  
                                        phone.Complete_reason="Exhaust Group Quota Total Time" ;
                                Message(phone.Complete_reason) ;
                    WriteScanStatistics(phone) ;
                                                             break ;
                                                                           }
                      }
/*- - - - - - - - - - - - - - - - - - - - - - - -  Определение целевого номера */
                   phone.Status=null ;

                   phone.Target=GetNextControl(true, phone.Extension,         /* Запрашиваем следующую команду управления ядра */
                                                 ref phone.QueueId   ) ;
                if(phone.Target=="Stop") {
                                            ScanCompleted=3 ;
                                                continue ;
                                         }
                if(phone.Target== null ) {                                    /* Если все номера перебраны */
                                           phone.Completed      =  1 ;  
                                           phone.Complete_reason="No more targets" ;
                                   Message(phone.Complete_reason) ;
                       WriteScanStatistics(phone) ;
                                                             break ;
                                         }
/*- - - - - - - - - - - - - - - - - -  Определение временных параметров звонка */
                                                active_time= DropActive ;
                if(RandomActive=="Uniform") {
                                                active_time=(int)(RobotActive+(DropActive-RobotActive)*Rand.NextDouble()) ;
                                            }
                if(RandomActive=="Top15"  ) {
                                          do {  
                                                active_time =(int)(RobotActive+(DropActive-RobotActive)*Rand.NextDouble()) ;
                                             } while(active_time%60<45 ||
                                                     active_time%60>57   ) ;  
                                            }

                                       phone.Active_drop=active_time ;

                                   phone.NextAttempt= DateTime.MinValue ;
                if(PulseActive>0)  phone.NextAttempt=(DateTime.Now).AddSeconds(active_time+DropActive*(1.0-PulseActive)*Rand.NextDouble()) ;
/*- - - - - - - - - - - - - - -  Контроль выхода звонка за время работы агента */
                if(DateTime.Now.AddSeconds(active_time)>complete_time) {      /* Если время звонка выxодит за время работы агента - */
                                                                              /*  - не звоним                                       */
                          Message(phone.Phone + " out of border") ;

                                        phone.Completed=3 ;
                                                break ;
                                                                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - Инициализация звонка */
                          Message(phone.Phone + " call " + phone.Target + "  max duration " + phone.Active_drop) ;

                if(Simulation==1) {                                           /* Эмуляция звонка */
                                    phone.CallStatus ="MonitorStopped" ;
                                    phone.Status     ="Normal" ;
                                    phone.NextAttempt=DateTime.Now ;
                    CheckOffControl(phone.QueueId) ;                          /* Удаляем номер из очереди команд */
                                          break ;  
                                  }

                   phone.Link=iface.MakeCall(phone.Phone, phone.Target) ;     /* Инициализируем соединение */
                if(phone.Link==null) {                                        /* Если ошибка... */

                          Message(phone.Phone + " error") ;
                          Message(iface.Error) ;

                                        phone.NextAttempt=(DateTime.Now).AddSeconds(ERROR_PAUSE) ;
                                        phone.Status     ="Error" ;
                                        phone.Error      ="Make Call: " + iface.Error ;
                    WriteScanStatistics(phone) ;
                                        phone.Target     =null ;
                                        phone.Completed  =  2 ;
                                         break ;
                                     }

                                      CheckOffControl(phone.QueueId) ;        /* Удаляем номер из очереди команд */

                                        phone.Status     ="Normal" ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                    } while(false) ;

                   if(phone.Target==null)  continue ;                         /* Если звонок не произведен... */

/*------------------------------------------------- Определение статуса звонка */

              current_time=DateTime.Now ;

  do {
       if(Simulation==1) {                                                    /* Эмуляция звонка */
                             times.InitTime     =current_time ;
                             times.DeliveryTime =current_time ;
                             times.ConnectTime  =current_time ;
                             times.ClearTime    =current_time ;
                             phone.Delivery_time=  5 ;
                             phone.Connect_time =  5 ;
                             phone.Active_time  =(current_time-phone.NextAttempt).Seconds ;
                                       break ;
                         }

              phone.CallStatus=iface.GetCallStatus(phone.Phone, ref times, ref phone.Events) ;

       if(phone.Status!="Normal")  break ;

                                                   phone.Delivery_time=0 ;
                                                   phone.Connect_time =0 ;
                                                   phone.Active_time  =0 ;

       if(times.DeliveryTime!=DateTime.MinValue)   phone.Delivery_time=(int)(times.DeliveryTime-times.InitTime).TotalSeconds ;
       else                                      {
                                                   phone.Delivery_time=(int)(      current_time-times.InitTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ConnectTime !=DateTime.MinValue)   phone.Connect_time =(int)(times.ConnectTime-times.DeliveryTime).TotalSeconds ;
       else                                      {
                                                   phone.Connect_time =(int)(     current_time-times.DeliveryTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ClearTime   !=DateTime.MinValue)   phone.Active_time  =(int)(times.ClearTime-times.ConnectTime).TotalSeconds ;
       else                                        phone.Active_time  =(int)(   current_time-times.ConnectTime).TotalSeconds ;

     } while(false) ;

/*---------------------------------- Контроль длительности до входящего звонка */

    if(phone.Status=="Normal")
     if(times.DeliveryTime==DateTime.MinValue)
      if(phone.Delivery_time>DropDelivery) {

                          Message(phone.Phone + " dropped by time (not deliveried)") ;

                                              phone.Status="Offline" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                           }
/*---------------------------------------------- Контроль длительности дозвона */

    if(phone.Status=="Normal")
      if(phone.Active_time>phone.Active_drop) {

                          Message(phone.Phone + " dropped by time (too long)") ;

                                              phone.Status="TooLong" ;
                               iface.DropCall(phone.Phone) ;

                           if(Simulation==1)  phone.CallStatus="ReadyForNext" ;

                                                 continue ;
                                              }
/*---------------------------------------------- Контроль длительности дозвона */

    if(phone.Status=="Normal")
     if(times.ConnectTime==DateTime.MinValue)
      if(phone.Connect_time>DropConnect) {

                          Message(phone.Phone + " dropped by time (not connected)") ;

                                              phone.Status="Ignored" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                         }
/*---------------------------------------------------------- Завершение звонка */

     if(phone.CallStatus=="ReadyForNext") {

                          Message(phone.Phone + " completed") ;

       if(phone.Status=="Normal" ||
          phone.Status=="TooLong"  ) {

        if(phone.Connect_time<=RobotConnect &&
           phone.Active_time >=RobotActive    )  WriteScanRobots(phone) ;

                        phone.Quota.Calls_Crn_Trip ++ ;
                        phone.Quota.Times_Crn_Trip +=phone.Active_time ;
                        phone.Quota.Calls_Crn_Total++ ;
                        phone.Quota.Times_Crn_Total+=phone.Active_time ;

                               grp=GetQuotaGroup(phone.Quota) ;
           if(grp>=0) {
                        Groups[grp].Calls_Crn_Trip ++ ;
                        Groups[grp].Times_Crn_Trip +=phone.Active_time ;
                        Groups[grp].Calls_Crn_Total++ ;
                        Groups[grp].Times_Crn_Total+=phone.Active_time ;
                      }
                                     } 

                                             WriteScanStatistics(phone) ;
                                                                 phone.Target=null ;

                                                    continue ;
                                          } 
/*---------------------------------------------------------- Цикл сканирования */

                                           }

     if(ScanCompleted!=3) {
                                             ScanCompleted=2 ;

        foreach(Station phone in Stations) {
                     if(phone.Completed==0)  ScanCompleted=0 ;
                     if(phone.Completed==1)  ScanCompleted=1 ;
                                           }
                          }

     if(ScanCompleted==1)
      if(complete_time>DateTime.Now) {                                        /* Если время работы не вышло... */
 
                                    Thread.Sleep(5000) ;

//                         action=GetNextControl(true) ;                      /* Запрашиваем следующую команду ядра управления */
//       if(String.Compare(action, "Stop", true)!=0) {                        /* Если нет команды остановки - ожидаем других команд */                                           
                   foreach(Station phone in Stations)  phone.Completed=0 ;
                                                         ScanCompleted=0 ;
//                                                   }
                                     }

        if(QuotaCycle>0)                                                      /* Обновление файла квот */
         if(quota_time<DateTime.Now) {
                                             QuotaFileCheck(false) ;
                         quota_time=quota_time.AddMinutes(QuotaCycle) ;       /* Определяем время следующего обновления файла квот */
                                     }

      if(abort_time<DateTime.Now) {                                           /* Если пройдена точка безусловного завершения... */
            Message("Abort time reached - force disconnection.") ;
                                     ScanCompleted=1 ;
                                  }

       } while(ScanCompleted==0) ;

/*------------------------------------------------------- Главный рабочий цикл */

      } while(false) ;                                                        /* BLOCK MAIN */

/*--------------------------------------------- Удаление виртуальных телефонов */

                         Message("Delete Phones...") ;

     if(Simulation==0) {

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*------------------------------------------------------ Отключение от сервера */

              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }
/*----------------------------------------------------------------- Завершение */

             QuotaFileCheck(false) ;

                TraceOnly=1 ;

/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                        Обзвон по списку "в касание"                         */

static void Kick()

{
          DMCC_this  iface ;
            Boolean  error ;
           DateTime  time ;
            Process  proc ;
            Boolean  start_agent ;
            Boolean  calls_over ;         /* Флаг полного перебора заданного набора номеров */
                int  status ;
                int  quota ;
                int  count ;
     ConsoleKeyInfo  chr ;
           DateTime  agent_time ;
           DateTime  generate_time ;
           DateTime  complete_time ;
           DateTime  calendar_time ;
           string[]  calls ;
             string  target ;

/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations     ==null) {
                              MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                                return ;
                            }
    if(ScanType     ==null) {
                              MessageWait("ERROR Configuration - <ScanType> specificator is missed") ;
                                return ;
                            }
    if(ScanNumbers  ==null) {
                              MessageWait("ERROR Configuration - <ScanNumbers> specificator is missed") ;
                                return ;
                            }
    if(ControlFolder==null) {
                              MessageWait("ERROR Configuration - <ControlFolder> specificator is missed") ;
                                return ;
                            }
    if(DropDelivery ==   0) {
                              MessageWait("ERROR Configuration - <DropDelivery> specificator is missed") ;
                                return ;
                            }
    if(CalendarPath ==null) {
                              MessageWait("ERROR Configuration - <CalendarPath> specificator is missed") ;
                                return ;
                            }

    if(ScanType     !="Single" && 
       ScanType     !="Heap"     ) {
                             MessageWait("ERROR Configuration - <ScanType> mast be 'Single' or 'Heap'") ;
                               return ;
                                   }

/*----------------------------------------------------------------- Подготовка */
 
                       Message("KICK mode\r\n") ;
    if(Simulation==1)  Message("SIMULATION mode\r\n") ;

    if(ActiveTime>0)  complete_time=DateTime.Now.AddMinutes(ActiveTime) ;     /* Определяем момент окончания работы */
    else              complete_time=DateTime.Now.AddMinutes(24*60) ;

       status=WriteScanStatistics(null) ;                                     /* Проверка записи общей статистики */
    if(status<0)  return ;

/*------------------------------------ Проверка создания виртуальных телефонов */

                             error=false ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Соединение с сервером */
             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Kick" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }

                          }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

                         Message("Create Phones...") ;

        foreach(Station phone in Stations) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                                  break ;
                            }
                                           }

                         Message("Delete Phones...") ;

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {

        foreach(Station phone in Stations)  phone.Phone=phone.Idx ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Отключение от сервера */
              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }

          if(error)  return ; 

               MessageWait("SUCCESS Create Phones\r\n") ;

/*------------------------------------------- Формирование списка сканирования */

               Targets    =new Target[TARGETS_MAX] ;
               Targets_cnt= 0 ;

      status=FormTargetsByFile(ControlFolder+"\\targets.save", false) ;       /* Восстанавливаем сохраненный список */
   if(status!=0) {                                                            /* Создаем новый список */

        ScanNumbers=ScanNumbers.Trim(',') ;

     if(ScanNumbers.Substring(0,1)=="@")  status=FormTargetsByFile  (ScanNumbers.Substring(1), true) ;
     else                                 status=FormTargetsByRanges(ScanNumbers) ;

     if(status<0)  return ;

                        ResetNextTarget(null) ;                                /* Инициализируем диапазон сканирования */
                 }

/*---------------------------------------------------------- Инициализация квот */

     foreach(Station phone in Stations) {  phone.Quota.Calls_Crn_Trip=0 ;
                                           phone.Quota.Times_Crn_Trip=0 ;  }

                 QuotaFileCheck(false) ;


/*---------------------------------------------------------- Цикл сканирования */

                            InitControl() ;                                   /* Инициализируем параметры управления агентом */
                        ResetNextTarget(null) ;                               /* Инициализируем диапазон сканирования */

                ScanCompleted=       0 ;

                calendar_time=DateTime.Now.AddMinutes(CalendarCycle) ;        /* Определяем время первого обновления файла календаря */
                   agent_time=DateTime.Now  ;
                generate_time=DateTime.Now  ;
                   calls_over=false ;
                       quota = 0 ;

     do {
                  Thread.Sleep(1000) ;

                    time=DateTime.Now ;

/*------------------------------------------------ Обработка консольных команд */

       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                       AddControl("Urgent", "Stop") ;
                                            ScanCompleted=1 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        
                                             ) ; 
                                     }
                                   } 
/*------------------------------------------------- Контроль завершения работы */
/*- - - - - - - - - - - - - - - - - - - - -  Проверка по общему времени работы */
     if(ActiveTime>0)                                                         /* Если задано время работы и оно вышло - */
      if(DateTime.Now>complete_time) {                                        /*   - завершаем работу                   */  
                                       AddControl("Urgent", "Stop") ;
                                          Message("Active time is over\r\n") ;
                                                     break ;
                                     }
/*------------------------------------------------- Обновление файла календаря */

        if(CalendarCycle>0)
         if(calendar_time<DateTime.Now) {
                                           CalendarFileCheck() ;
                     calendar_time=calendar_time.AddMinutes(CalendarCycle) ;
                                        }
/*------------------------------------------ Управление режимом работы агентов */

                                            start_agent=false ;

        if(time>=agent_time) {                                                 /* Квантование периодов запуска Агента */
                              agent_time=agent_time.AddSeconds(AgentPeriod) ;
                                            start_agent=true ;
                             }
/*---------------------------------------------------------- Генерация номеров */

        if(time>=generate_time) {                                              /* Квантуем периоды генерации номеров */  

                      generate_time=generate_time.AddSeconds(GeneratePeriod) ;

             quota=CalendarQuota(time) ;                                      /* Определение режима периода */ 
          if(quota <0)  quota=6000 ;

             quota/=3600/GeneratePeriod ;                                     /* Переситываем квоту на период генерации */

          if(quota>0)                                                         /* Если активный режим... */
           if(generate_time<=agent_time) {                                    /* Звонки генерятся кроме последнего периода */

                    calls=Directory.GetFiles(ControlFolder, "*.call") ;       /* Определяем число звонков "в стеке" */
                    count=quota-calls.Count() ;                               /* Определяем, сколько звонков надо сгенерить */

             if(calls.Count()==0 && calls_over) {                             /* Если мы ожидали завершения перебора номеров... */
                                          Message("Numbers list is over") ;
                                                    ScanCompleted=1 ;
                                                        break ;
                                                }

             if(count>0) {
                            Message("Generate "+count+" calls") ;

               for( ; count>0 ; count--) {

                     target=GetNextTarget(null) ;                             /* Запрашиваем следующий номер */
                  if(target==null) {                                          /* Если все номера перебраны */
                                      ResetNextTarget(null) ;
                                          count++ ;
                                   }
                  else             {
                                       AddControl("Queue", target) ;
                                   }
                                         }

                      SaveTargetsToFile(ControlFolder+"\\targets.save") ;     /* Сохранение состояния целевого пула */
                         }
             else        {
                            Message("No calls generated") ;
                         }
                                         }
                                }
/*--------------------------------------------------- Запуск агента исполнения */

        if(start_agent) {

                            Message("Start AGENT for "+AgentPeriod+" seconds") ;

               proc                          =new Process() ;
               proc.StartInfo.FileName       =Environment.CommandLine.Substring(0, Environment.CommandLine.IndexOf(' '));
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Trim('\"');
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Replace(".vshost", "");
               proc.StartInfo.Arguments      ="Kick "+CfgPath+" Agent" ;
               proc.StartInfo.UseShellExecute= false ;

          try 
          {
               proc.Start() ;
          }
          catch (Exception exc)
          {
              Message("Calls processor start error: "+exc.Message) ;
          }

               proc=null ;

                        }
/*---------------------------------------------------------- Цикл сканирования */

        } while(ScanCompleted==0) ;

/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");

}
/*******************************************************************************/
/*                                                                             */
/*                               Агент KICK-обзвона                            */

static void SingleKick()

{
          DMCC_this  iface ;
   DMCC_phone_times  times ;
           DateTime  current_time ;
            Boolean  error ;
                int  status ;
           DateTime  complete_time ;
           DateTime  quota_time ;
             string  action ;
                int  grp ;

/*----------------------------------------------------------------- Подготовка */

                              times=new DMCC_phone_times() ;

                        Targets    =new Target[TARGETS_MAX] ;
                        Targets_cnt= 0 ;

                          TraceOnly= 0 ;

                        Log.MaxSize= 0 ;
  
                      complete_time=DateTime.Now.AddMinutes(AgentPeriod-60) ;
                         quota_time=DateTime.Now.AddMinutes(QuotaCycle) ;     /* Определяем время первого обновления файла квот */

/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="SingleKick" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("SingleKick - Connecting...") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           Message("SingleKick - ERROR - Connect:\r\n " + iface.Error + "\r\n") ;
                              return ;
                        }

                          }
/*------------------------------------------------------- Главный рабочий цикл */

   do {                                                                       /* BLOCK MAIN */

/*--------------------------------------------- Создание виртуальных телефонов */

                         Message("Create Phones...") ;

                                  error=true ;

        foreach(Station phone in Stations) {

          if(Simulation==1) {
                                phone.Phone=phone.Idx ;
                                      error=false ;
                                       continue ;
                            }  

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               phone.Error="Phone creation error" ;
                            }
          else              {
                                  error=false ;
                            }
                                           }

          if(error)  break ; 

                           MessageWait("SUCCESS Create Phones\r\n") ;

/*---------------------------------------------------------- Цикл сканирования */

     do {
                       Thread.Sleep(100) ;

        foreach(Station phone in Stations) {

/*------------------------------------------------ Обработка консольных команд */
#if REMARK
       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                            ScanCompleted=3 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        
                                             ) ; 
                                     }
                                   } 
#endif
/*---------------------------------------------- Обработка экстренных сигналов */

            action=GetNextControl(false, null, ref NULL) ;                    /* Запрашиваем налиюие сигналов */
         if(action=="Stop") {
                                ScanCompleted=3 ;
                            }

         if(ScanCompleted==3) {
                                        Message(phone.Phone + " stopped by user") ;

                                                phone.Status="UserBreaking" ;
                                 iface.DropCall(phone.Phone) ;
                              }
/*----------------------------------------------------------- Инициация звонка */

         if(phone.Completed==0)  
          if(phone.Target==null) do {
/*- - - - - - - - - - - - - - - - - - - - - - - -  Контроль времени завершения */
                if(complete_time<DateTime.Now) {                              /* Если время работы вышло - "закрываем" сканирование */
                                   Message("ACTIVE TIME completed") ;
                                                 phone.Completed=1 ;
                                                    break ;
                                               }
/*- - - - - - - - - - - - - - - - - - - - - - - -  Определение целевого номера */
                   phone.Status=null ;

                   phone.Target=GetNextControl(true, phone.Extension,         /* Запрашиваем следующую команду управления ядра */
                                                 ref phone.QueueId   ) ;
                if(phone.Target=="Stop") {
                                            ScanCompleted=3 ;
                                                continue ;
                                         }
                if(phone.Target==null) {                                      /* Если все номера перебраны */
                                         phone.Completed      =  1 ;  
                                         phone.Complete_reason="No more targets" ;
                                 Message(phone.Complete_reason) ;
                     WriteScanStatistics(phone) ;
                                                             break ;
                                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - Инициализация звонка */
                          Message(phone.Phone + " call " + phone.Target) ;

                if(Simulation==1) {                                           /* Эмуляция звонка */
                                    phone.CallStatus="MonitorStopped" ;
                                    phone.Status    ="Normal" ;
                    CheckOffControl(phone.QueueId) ;                          /* Удаляем номер из очереди команд */
                                      Thread.Sleep(1000) ;
                                          break ;  
                                  }

                   phone.Link=iface.MakeCall(phone.Phone, phone.Target) ;     /* Инициализируем соединение */
                if(phone.Link==null) {                                        /* Если ошибка... */

                          Message(phone.Phone + " error") ;
                          Message(iface.Error) ;

                                        phone.Status     ="Error" ;
                                        phone.Error      ="Make Call: " + iface.Error ;
                    WriteScanStatistics(phone) ;
                                        phone.Target     =null ;
                                        phone.Completed  =  2 ;
                                         break ;
                                     }

                                      CheckOffControl(phone.QueueId) ;        /* Удаляем номер из очереди команд */

                                        phone.Status     ="Normal" ;
                                        phone.Active_drop= DropActive ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                    } while(false) ;

                   if(phone.Target==null)  continue ;                         /* Если звонок не произведен... */

/*------------------------------------------------- Определение статуса звонка */

              current_time=DateTime.Now ;

  do {
       if(Simulation==1) {                                                    /* Эмуляция звонка */
                             times.InitTime     =current_time ;
                             times.DeliveryTime =current_time ;
                             times.ConnectTime  =current_time ;
                             times.ClearTime    =current_time ;
                             phone.Delivery_time=  5 ;
                             phone.Connect_time =  5 ;
                             phone.Active_time  = 30 ;
                             phone.CallStatus   ="ReadyForNext" ;
                                       break ;
                         }

              phone.CallStatus=iface.GetCallStatus(phone.Phone, ref times, ref phone.Events) ;

       if(phone.Status!="Normal")  break ;

                                                   phone.Delivery_time=0 ;
                                                   phone.Connect_time =0 ;
                                                   phone.Active_time  =0 ;

       if(times.DeliveryTime!=DateTime.MinValue)   phone.Delivery_time=(int)(times.DeliveryTime-times.InitTime).TotalSeconds ;
       else                                      {
                                                   phone.Delivery_time=(int)(      current_time-times.InitTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ConnectTime !=DateTime.MinValue)   phone.Connect_time =(int)(times.ConnectTime-times.DeliveryTime).TotalSeconds ;
       else                                      {
                                                   phone.Connect_time =(int)(     current_time-times.DeliveryTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ClearTime   !=DateTime.MinValue)   phone.Active_time  =(int)(times.ClearTime-times.ConnectTime).TotalSeconds ;
       else                                        phone.Active_time  =(int)(   current_time-times.ConnectTime).TotalSeconds ;

     } while(false) ;

/*---------------------------------- Контроль длительности до входящего звонка */

    if(phone.Status=="Normal")
     if(times.DeliveryTime==DateTime.MinValue)
      if(phone.Delivery_time>DropDelivery) {

                          Message(phone.Phone + " dropped by time (not deliveried)") ;

                                              phone.Status="Offline" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                           }
/*------------------------------------------------- Реакция на входящий звонок */

    if(phone.Status=="Normal")
     if(times.DeliveryTime!=DateTime.MinValue) {

                          Message(phone.Phone + " kick on") ;

                                              phone.Status="Delivered" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                               }
/*---------------------------------------------------------- Завершение звонка */

     if(phone.CallStatus=="ReadyForNext") {

                          Message(phone.Phone + " completed") ;

       if(phone.Status=="Normal" ||
          phone.Status=="TooLong"  ) {

                        phone.Quota.Calls_Crn_Trip ++ ;
                        phone.Quota.Times_Crn_Trip +=phone.Active_time ;
                        phone.Quota.Calls_Crn_Total++ ;
                        phone.Quota.Times_Crn_Total+=phone.Active_time ;

                               grp=GetQuotaGroup(phone.Quota) ;
           if(grp>=0) {
                        Groups[grp].Calls_Crn_Trip ++ ;
                        Groups[grp].Times_Crn_Trip +=phone.Active_time ;
                        Groups[grp].Calls_Crn_Total++ ;
                        Groups[grp].Times_Crn_Total+=phone.Active_time ;
                      }
                                     } 

                                             WriteScanStatistics(phone) ;
                                                                 phone.Target=null ;

                                                    continue ;
                                          } 
/*---------------------------------------------------------- Цикл сканирования */

                                           }

     if(ScanCompleted!=3) {
                                             ScanCompleted=2 ;

        foreach(Station phone in Stations) {
                     if(phone.Completed==0)  ScanCompleted=0 ;
                     if(phone.Completed==1)  ScanCompleted=1 ;
                                           }
                          }

     if(ScanCompleted==1)
      if(complete_time>DateTime.Now) {                                        /* Если время работы не вышло... */
 
                                    Thread.Sleep(5000) ;

//                           action=GetNextControl(true) ;                    /* Запрашиваем следующую команду ядра управления */
//         if(String.Compare(action, "Stop", true)!=0) {                      /* Если нет команды остановки - ожидаем других команд */                                           
                   foreach(Station phone in Stations)  phone.Completed=0 ;
                                                         ScanCompleted=0 ;
//                                                     }
                                     }

        if(QuotaCycle>0)                                                      /* Обновление файла квот */
         if(quota_time<DateTime.Now) {
                                             QuotaFileCheck(false) ;
                         quota_time=quota_time.AddMinutes(QuotaCycle) ;       /* Определяем время следующего обновления файла квот */
                                     }

       } while(ScanCompleted==0) ;

/*------------------------------------------------------- Главный рабочий цикл */

      } while(false) ;                                                        /* BLOCK MAIN */

/*--------------------------------------------- Удаление виртуальных телефонов */

                         Message("Delete Phones...") ;

     if(Simulation==0) {

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*------------------------------------------------------ Отключение от сервера */

              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }
/*----------------------------------------------------------------- Завершение */

             QuotaFileCheck(false) ;

                TraceOnly=1 ;

/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                 Обзвон по списку с реализацией обратной связи               */

static void Play()

{
          DMCC_this  iface ;
  FileSystemWatcher  ctrl_watcher ;
       StreamReader  file ;
            Boolean  error ;
             string  target ;
             string  text ;
            Process  proc ;
                int  status ;
     ConsoleKeyInfo  chr ;
                int  n ;
                int  i ;
 
/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations     ==null) {
                              MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                                return ;
                            }
    if(TargetsPath  ==null) {
                              MessageWait("ERROR Configuration - <TargetsPath> specificator is missed") ;
                                return ;
                            }
    if(ControlFolder==null) {
                              MessageWait("ERROR Configuration - <ControlFolder> specificator is missed") ;
                                return ;
                            }
    if(DropDelivery ==   0) {
                              MessageWait("ERROR Configuration - <DropDelivery> specificator is missed") ;
                                return ;
                            }
    if(DropConnect  ==   0) {
                              MessageWait("ERROR Configuration - <DropConnect> specificator is missed") ;
                                return ;
                            }
    if(TalkFile     ==null) {
                              MessageWait("ERROR Configuration - <TalkFile> specificator is missed") ;
                                return ;
                            }
/*----------------------------------------------------------------- Подготовка */
 
                             Message("PLAY mode\r\n") ;
          if(Simulation==1)  Message("SIMULATION mode\r\n") ;

       status=WriteScanStatistics(null) ;                                     /* Проверка записи общей статистики */
    if(status<0)  return ;

       status=WriteResults(null) ;                                            /* Проверка записи результатов */
    if(status<0)  return ;

/*------------------------------------ Проверка создания виртуальных телефонов */

                             error=false ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Соединение с сервером */
             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Play" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }

                          }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

                         Message("Create Phones...") ;

        foreach(Station phone in Stations) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                                  break ;
                            }
                                           }

                         Message("Delete Phones...") ;

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {

        foreach(Station phone in Stations)  phone.Phone=phone.Idx ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Отключение от сервера */
              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }

          if(error)  return ; 

               MessageWait("SUCCESS Create Phones\r\n") ;

/*-------------------------------------------- Подготовка регистратора событий */

          ctrl_watcher                    =new FileSystemWatcher() ;
          ctrl_watcher.Path               = ControlFolder ;
          ctrl_watcher.Filter             ="*.*"  ;
          ctrl_watcher.NotifyFilter       = NotifyFilters.FileName ;
          ctrl_watcher.Created           += new FileSystemEventHandler(iPlay_CtrlDetection);
          ctrl_watcher.EnableRaisingEvents= true ;

/*----------------------------------------------------- Открываем файл номеров */

   try 
   {
               file= new StreamReader(TargetsPath, System.Text.Encoding.GetEncoding(1251)) ;
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - targets file open error:\r\n"+exc.Message) ;
                          return ;
   }
/*---------------------------------------------------------- Цикл сканирования */

                ScanCompleted=0 ;

     do {
                  Thread.Sleep(1000) ;

/*------------------------------------------------ Обработка консольных команд */

       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                            ScanCompleted=1 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        
                                             ) ; 
                                     }
                                   } 
/*------------------------------------------------- Ожидание свободной станции */

                     n=-1 ;

          for(i=0 ; i<Stations.Count() ; i++)                                   /* Поиск свободной станции с минимальной загрузкой */
            if(Stations[i].Target==null) {

                  if(         n==-1                   )  n=i ;
             else if(Stations[i].Quota.Calls_Crn_Trip<
                     Stations[n].Quota.Calls_Crn_Trip )  n=i ;
                                         }

           if(n<0)  continue ;

/*----------------------------------------------------------- Считывание файла */

             text=file.ReadLine() ;
          if(text==null)  break ;

             text=text.Replace('\t', ' ') ;
             text=text.Trim() ;

          if(text               =="" )  continue ;
          if(text.Substring(0,1)==";")  continue ;

                   target=text ;

/*---------------------------------------------------------- Исполнение вызова */

                    Message("Play executed: "+target) ;

              Stations[n].Target     = target ;
//            Stations[n].NextAttempt=(DateTime.Now).AddSeconds(HANGUP_PAUSE) ;
              Stations[n].Quota.Calls_Crn_Trip++ ;

               proc                          =new Process() ;
               proc.StartInfo.FileName       =Environment.CommandLine.Substring(0, Environment.CommandLine.IndexOf(' '));
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Trim('\"');
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Replace(".vshost", "");
               proc.StartInfo.Arguments      ="Play "+CfgPath+" "+Stations[n].Extension+" \""+target+"\"" ;
               proc.StartInfo.UseShellExecute= false ;

          try 
          {
               proc.Start() ;
          }
          catch (Exception exc)
          {
              Message("Call processor start error: "+exc.Message) ;
          }

               proc=null ;

/*---------------------------------------------------------- Цикл сканирования */

        } while(ScanCompleted==0) ;

/*------------------------------------------------------------- Закрытие файла */

               file.Close() ;

/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");

}

private static void iPlay_CtrlDetection(object source, FileSystemEventArgs e)
{
  string  extension ;


             extension=e.Name.Substring(0, e.Name.IndexOf('.')) ;

          Message("Control request detected: "+e.Name) ;

   if(String.Compare(extension, "Stop",  true)==0)
   {
                    ScanCompleted=1 ;   
   }
   else
   if(e.Name.IndexOf(".err")>=0)
   {
        foreach(Station phone in Stations)
          if(String.Compare(phone.Extension, extension,  true)==0) {
                                       phone.Target=null ;
                                                                   }
   }
   else
   if(e.Name.IndexOf(".rel")>=0)
   {
        foreach(Station phone in Stations)
          if(String.Compare(phone.Extension, extension,  true)==0) {
                                       phone.Target=null ;
                                                                   }
   }

        Thread.Sleep (100) ;
          File.Delete(e.FullPath) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ветвь отработки одиночной серии звонков                     */

static void SinglePlay(string  extension, string  target)

{
             string  flag_id ;
          DMCC_this  iface ;
            Station  phone ;
   DMCC_phone_times  times ;
  DMCC_phone_events  events ;
           DateTime  current_time ;
            Boolean  error ;
            Boolean  done ;           /* Флаг установления соединения */
            Boolean  t_established ;
            Boolean  t_failed ;
        TonesAction  action ;
            Process  proc ;
                int  status ;
             string  name ;
                int  pos ;
             string  tones ;
             string  reply ;
             string  result ;
                int  n ;
                int  i ;

/*----------------------------------------------------------------- Подготовка */

                TraceOnly= 0 ;

              Log.MaxSize= 0 ;

                   result="" ;

/*-------------------------------------------------- Разбор целевого параметра */
/* - - - - - - - - - - - - - - - - - - - - - - - Выделение сигнального индекса */
       pos=target.IndexOf(':') ;
    if(pos>0) {
                  flag_id=target.Substring(pos+1) ; 
                   target=target.Substring(0, pos) ;
                        n= -1 ;

            for(i=0 ; i<FlagFileActions_cnt ; i++)
              if(FlagFileActions[i].id==flag_id) {  n=i ;  break ;  }

              if(n>=0) {
                             TalkFile=FlagFileActions[n].talk_file ;

                for(i=0 ; FlagFileActions[n].actions[i]!=null ; i++) {
                                TonesActions[i]=FlagFileActions[n].actions[i] ;
                                TonesActions_cnt++ ;              
                                                                     }

                       }          
              }
/* - - - - - - - - - - - - - - - - - - - - - - - - - - - Разбор списка номеров */
       pos=target.IndexOf(';') ;
    if(pos<0) {
                        WriteResults(target+";Illegal record format") ;
                 iPlay_ReleaseSignal(extension, "") ;                         /* Создаем сигнал освобождения телефона */
                         TraceOnly=1 ;
                             return ;
              }

              name=target.Substring(0, pos) ; 
       ScanNumbers=target.Substring(pos+1) ;

/*------------------------------------------ Построение списка номеров дозвона */

                                  Targets    =new Target[100] ;
                                  Targets_cnt= 0 ;

                                  ScanNumbers=ScanNumbers.Replace(';', ',') ;
                                  ScanNumbers=ScanNumbers.Trim(',') ;
       status=FormTargetsByRanges(ScanNumbers) ;
    if(status<0) {
                        WriteResults(name+";Pnones list is empty") ;
                 iPlay_ReleaseSignal(extension, "") ;                         /* Создаем сигнал освобождения телефона */
                         TraceOnly=1 ;
                             return ;
                 }
/*---------------------------------------- Идентификация виртуального телефона */

                                                                phone=null ;

   foreach(Station phone_ in Stations)
     if(String.Compare(phone_.Extension, extension,  true)==0)  phone=phone_ ;

     if(phone==null) {
                        Message("SinglePlay - ERROR unknown extension: "+extension) ;
              iPlay_ErrorSignal( extension, "Unknown extension") ;            /* Создаем сигнал отключения телефона */                                                 
                                     return ;
                     }
/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="SinglePlay" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("SinglePlay - Connecting...") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           Message("SinglePlay - ERROR - Connect:\r\n " + iface.Error + "\r\n") ;
                 iPlay_ErrorSignal( extension, "Connect: "+iface.Error) ;     /* Создаем сигнал отключения телефона */                                                 
                              return ;
                        }

                          }
/*--------------------------------------------- Создание виртуального телефона */

                         Message("SinglePlay - Create Phone...") ;

                             error=false ;
                              done=false ;
                             times=new DMCC_phone_times() ;
                            events=new DMCC_phone_events() ;  

                     t_established=false ;
                          t_failed=false ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               Message("SinglePlay - ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                                 "Extension :" + phone.Extension  + "\r\n" +
                                                                 "Password  :" + phone.Password   + "\r\n" +
                                                                 "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                                 "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                            }
                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {
                              phone.Phone=phone.Idx ;
                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Обработка ошибок */
     if(error) {
                 iPlay_ErrorSignal(extension, "CreatePhone: "+iface.Error) ;  /* Создаем сигнал отключения телефона */                                                 
               }
     else      {
                           Message("SinglePlay - SUCCESS Create Phones") ;
               }  
/*---------------------------------------------------------- Цикл сканирования */

                          reply="Unknown" ;

                            InitControl() ;                                   /* Инициализируем параметры управления агентом */
                        ResetNextTarget(null) ;                               /* Инициализируем диапазон сканирования */

     if(phone.Phone>=0)  do {

                       Thread.Sleep(100) ;

/*----------------------------------------------------------- Инициация звонка */
                                  
          do {
                   phone.Status=null ;

                   phone.Target=GetNextTarget(null) ;                         /* Запрашиваем следующий номер */
                if(phone.Target==null) {                                      /* Если все номера перебраны */
                                          ScanCompleted=1 ;
                                             break ;
                                       }

                          Message(phone.Phone + " call " + phone.Target) ;

                if(Simulation==1) {                                           /* Эмуляция звонка */
                                    phone.CallStatus="MonitorStopped" ;
                                    phone.Status    ="Normal" ;
                                      Thread.Sleep(1000) ;
                                          break ;  
                                  }

                   phone.Link=iface.MakeCall(phone.Phone, phone.Target) ;     /* Инициализируем соединение */
                if(phone.Link==null) {                                        /* Если ошибка... */

                          Message(phone.Phone + " error") ;
                          Message(iface.Error) ;

                                        phone.NextAttempt=(DateTime.Now).AddSeconds(ERROR_PAUSE) ;
                                        phone.Status     ="Error" ;
                                        phone.Error      ="Make Call: " + iface.Error ;
                    WriteScanStatistics(phone) ;
                                        phone.Target     =null ;
                                         break ;
                                     }

                                             phone.Status="Normal" ;

             } while(false) ;

                   if(ScanCompleted==  1 )  continue ;                        /* Если перебран весь список ... */
                   if(phone.Target ==null)  continue ;                        /* Если звонок не произведен... */

/*-------------------------------------------- Цикл ожидания завершения звонка */
 
   do {                                                                       /* LOOP */
                  Thread.Sleep(100) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - Определение статуса звонка */
                              current_time=DateTime.Now ;

    do {
         if(Simulation==1) {                                                  /* Эмуляция звонка */
                              times.InitTime     =current_time ;
                              times.DeliveryTime =current_time ;
                              times.ConnectTime  =current_time ;
                              times.ClearTime    =current_time ;
                             events.Established  =true ;
                              phone.Delivery_time=  5 ;
                              phone.Connect_time =  5 ;
                              phone.CallStatus   ="ReadyForNext" ;
                                       break ;
                           }

              phone.CallStatus=iface.GetCallStatus(phone.Phone, ref times, ref events) ;

       if(phone.Status!="Normal")  break ;

                        phone.Delivery_time=0 ;
                        phone.Connect_time =0 ;
                        phone.Active_time  =0 ;

       if(times.DeliveryTime!=DateTime.MinValue)   phone.Delivery_time=(int)(times.DeliveryTime-times.InitTime).TotalSeconds ;
       else                                      {
                                                   phone.Delivery_time=(int)(      current_time-times.InitTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ConnectTime !=DateTime.MinValue)   phone.Connect_time =(int)(times.ConnectTime-times.DeliveryTime).TotalSeconds ;
       else                                      {
                                                   phone.Connect_time =(int)(     current_time-times.DeliveryTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ClearTime   !=DateTime.MinValue)   phone.Active_time  =(int)(times.ClearTime-times.ConnectTime).TotalSeconds ;
       else                                        phone.Active_time  =(int)(   current_time-times.ConnectTime).TotalSeconds ;

     } while(false) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - -  Установление соединения */
    if(phone.Status=="Normal")
     if(events.Established==true) {

          if(!done) {
                          status=iface.MediaInitialize(phone.Phone, true) ;                           
                       if(status!=0) {
                                         Message("SinglePlay - Media monitor start ERROR") ;
                                         Message(iface.Error) ;
                                     } 
                          status=iface.MediaWaitTones(phone.Phone, 1, '\0', true) ;
                       if(status!=0) {
                                         Message("SinglePlay - Wait tones start ERROR") ;
                                         Message(iface.Error) ;
                                     } 
                          status=iface.MediaPlay(phone.Phone, TalkFile, true, true) ;
                       if(status!=0) {
                                         Message("SinglePlay - File playing start ERROR") ;
                                         Message(iface.Error) ;
                                     } 
                    } 

                                                done=true ;
                                  }
/*- - - - - - - - - - - - - - - - - - - - - Установление Transfered-соединения */
     if(events.Transfered==true) {

                          Message(phone.Phone + " transfered") ;

                              WriteScanStatistics(phone) ;

             if(!done)  phone.Target=null ;

                                                    break ;
                                 }
/*- - - - - - - - - - - - - - - - -  Контроль длительности до входящего звонка */
    if(phone.Status=="Normal")
     if(times.DeliveryTime==DateTime.MinValue)
      if(phone.Delivery_time>DropDelivery) {

                          Message(phone.Phone + " dropped by time (not deliveried)") ;

                                              phone.Status="Offline" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                           }
/*- - - - - - - - - - - - - - - - - - - Контроль длительности входящего звонка */
    if(phone.Status=="Normal")
     if(times.ConnectTime==DateTime.MinValue)
      if(phone.Connect_time>DropConnect) {

                          Message(phone.Phone + " dropped by time (not connected)") ;

                                              phone.Status="Ignored" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                         }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение звонка */
     if(phone.CallStatus=="ReadyForNext") {

                                          Message(phone.Phone + " completed") ;

                               iface.MediaRelease(phone.Phone) ;

                              WriteScanStatistics(phone) ;

             if(!done)  phone.Target=null ;

                                                    break ;
                                          }
/*- - - - - - - - - - - - - - - - - - - - - - - - -  Появление тонового набора */
    if(events.MediaTonesFlushed==true) {

                  tones=iface.MediaGetTones(phone.Phone) ;
               if(tones!=null) {

                    action=TonesControl("DIGIT", tones.Substring(0,1)) ;
                 if(action       ==  null   ) {
                                                     break ;
                                              }
                 else
                 if(action.action=="write"  ) { 
                                                Message(phone.Phone + " write <" + action.target + ">") ;

                                                       result= action.action ;
                                                        reply= action.target ;
                                                 phone.Status="Tones detected" ;
                                  iface.DropCall(phone.Phone) ;
                                                     break ;
                                              }
                 else 
                 if(action.action=="call"   ) { 
                                                Message(phone.Phone + " forwarding to " + action.target) ;

                                                           result= action.action ;
                                                            reply="call action.target" ;
                                  iface.TransferCall(phone.Phone, action.target, "Prepare") ;
                                              }
                 else
                 if(action.action=="execute") { 
                                                Message(phone.Phone + " execute " + action.target) ;

                                                       result= action.action ;
                                                        reply="execute "+action.target ;
                                                 phone.Status="Tones detected" ;
                                  iface.DropCall(phone.Phone) ;

                                                 proc                          =new Process() ;
                                                 proc.StartInfo.FileName       =action.target;
                                                 proc.StartInfo.Arguments      ="" ;
                                                 proc.StartInfo.UseShellExecute= false ;

                                                try {  proc.Start() ;  }
                                                catch (Exception exc)
                                                {
                                                   Message("Call processor start error: "+exc.Message) ;
                                                }
                                                        proc=null ;
                                              }
                 else                         {
                                                Message(phone.Phone + " unknown action - " + action.action) ;
                                                     break ;
                                            }
                               }
                                       }
/*- - - - - - - - - - - - - - - - - - - - - - Установление Transfer-соединения */
//  if(events.Established_t==true) {
    if(events.Delivered_t==true) {

         if(t_established==false) {
                  iface.TransferCall(phone.Phone, "", "Transfer") ;
                  iface.MediaRelease(phone.Phone) ;
                                  }

            t_established=true ;
                                   }   
/*- - - - - - - - - - - - - - - - - - - - - - - - -  Сброс Transfer-соединения */
    if(events.Failed_t==true) {

         if(t_failed==false)
                  iface.TransferCall(phone.Phone, "", "Drop") ;

            t_failed=true ;
                              }   
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
      } while(true) ;                                                         /* END LOOP */

                      if(done)  break ;                                       

/*---------------------------------------------------------- Цикл сканирования */

                            } while(ScanCompleted==0) ;

/*-------------------------------------------- Отлючение виртуального телефона */

     if(phone.Phone>=0) {

       if(Simulation==0) {
                             iface.DeletePhone(phone.Phone) ;
                         }

                        }
/*------------------------------------------------------ Отключение от сервера */

              Message("SinglePlay - Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("SinglePlay - ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }
/*-------------------------------------------------- Запись результата дозвона */

                WriteResults(name+";;"+phone.Target+";"+";"+reply) ;

/*----------------------------------------------------------------- Завершение */

                 iPlay_ReleaseSignal(extension, result) ;                     /* Создаем сигнал освобождения телефона */

                            TraceOnly=1 ;

/*-----------------------------------------------------------------------------*/

}

private static void iPlay_ErrorSignal(string extension, string text)
{
   File.WriteAllText(ControlFolder+"\\"+extension+".err", text) ;
}

private static void iPlay_ReleaseSignal(string extension, string result)
{
  string  info ;

                     info=extension ;
     if(result!="")  info=info + ";"+result ;

   File.WriteAllText(ControlFolder+"\\"+extension+".rel", info) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ветвь отработки запросов на звонок                          */

static void Call()

{
          DMCC_this  iface ;
  FileSystemWatcher  call_watcher ;
  FileSystemWatcher  ctrl_watcher ;
            Boolean  error ;
             string  target ;
            Process  proc ;
                int  status ;
     ConsoleKeyInfo  chr ;
                int  n ;
                int  i ;
 
/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations     ==null) {
                              MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                                return ;
                            }
    if(CallsFolder  ==null) {
                              MessageWait("ERROR Configuration - <CallsFolder> specificator is missed") ;
                                return ;
                            }
    if(ControlFolder==null) {
                              MessageWait("ERROR Configuration - <ControlFolder> specificator is missed") ;
                                return ;
                            }
    if(CallsFolder  ==
       ControlFolder      ) {
                              MessageWait("ERROR Configuration - <CallsFolder> and <ControlFolder> mast be different folders") ;
                                return ;
                            }
    if(ReCallSpec   ==null) {
                              MessageWait("ERROR Configuration - <ReCallSpec> specificator is missed") ;
                                return ;
                            }
    if(DropDelivery ==   0) {
                              MessageWait("ERROR Configuration - <DropDelivery> specificator is missed") ;
                                return ;
                            }
    if(DropConnect  ==   0) {
                              MessageWait("ERROR Configuration - <DropConnect> specificator is missed") ;
                                return ;
                            }
/*----------------------------------------------------------------- Подготовка */
 
                             Message("CALL mode\r\n") ;
          if(Simulation==1)  Message("SIMULATION mode\r\n") ;

                             error=false ;

       status=WriteScanStatistics(null) ;                                     /* Проверка записи общей статистики */
    if(status<0)  return ;

           Targets    =new Target[TARGETS_MAX] ;
           Targets_cnt= 0 ;
           Targets_new= 0 ;

/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Call" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }

                          }
/*------------------------------------------------------- Главный рабочий цикл */

   do {                                                                       /* BLOCK MAIN */

/*------------------------------------ Проверка создания виртуальных телефонов */
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

                         Message("Create Phones...") ;

        foreach(Station phone in Stations) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                                  break ;
                            }
                                           }

                         Message("Delete Phones...") ;

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                               if(error)  break ; 
                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {

        foreach(Station phone in Stations)  phone.Phone=phone.Idx ;

                       }

                           MessageWait("SUCCESS Create Phones\r\n") ;

/*------------------------------------------------------ Отключение от сервера */

              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }
/*-------------------------------------------- Подготовка регистратора событий */

          call_watcher                    =new FileSystemWatcher() ;
          call_watcher.Path               = CallsFolder ;
          call_watcher.Filter             ="*.call"  ;
          call_watcher.NotifyFilter       = NotifyFilters.FileName ;
          call_watcher.Created           += new FileSystemEventHandler(iCall_CallDetection);
          call_watcher.Deleted           += new FileSystemEventHandler(iCall_CallDetection);
          call_watcher.EnableRaisingEvents= true ;

          ctrl_watcher                    =new FileSystemWatcher() ;
          ctrl_watcher.Path               = ControlFolder ;
          ctrl_watcher.Filter             ="*.*"  ;
          ctrl_watcher.NotifyFilter       = NotifyFilters.FileName ;
          ctrl_watcher.Created           += new FileSystemEventHandler(iCall_CtrlDetection);
          ctrl_watcher.EnableRaisingEvents= true ;

/*---------------------------------------------------------- Цикл сканирования */

                ScanCompleted=0 ;

     do {
                  Thread.Sleep(1000) ;

/*------------------------------------------------ Обработка консольных команд */

       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                            ScanCompleted=1 ;
                                                 break ;
                                     }
         else
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Очередь звонков */
         if(chr.KeyChar.Equals('q')) {

                     Console.WriteLine("\r\nCalls in queue: " + Targets_cnt) ;

                for(i=0 ; i<Targets_cnt ; i++)
                     Console.WriteLine(Targets[i].Phone+"\t"+Targets[i].ReCallSpec) ;

                     Console.WriteLine("\r\nStations: ") ;
                for(i=0 ; i<Stations.Count() ; i++)                  
                     Console.WriteLine(Stations[i].Extension           +"\t"+
                                       Stations[i].Quota.Calls_Crn_Trip+"\t"+
                                       Stations[i].Target) ;

                     Console.WriteLine("") ;

                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n"        +
                                              "  q  -  calls queue\r\n\r\n"      
                                             ) ; 
                                     }
                                   } 
/*---------------------------------------------------------- Исполнение вызова */

                     n=-1 ;

          for(i=0 ; i<Stations.Count() ; i++)                                   /* Поиск свободной станции с минимальной загрузкой */
            if(Stations[i].Target==null) {

                  if(         n==-1                   )  n=i ;
             else if(Stations[i].Quota.Calls_Crn_Trip<
                     Stations[n].Quota.Calls_Crn_Trip )  n=i ;
                                         }

           while(n>=0) {
                          target=TargetsControl("Next", null) ;               /* Запрашиваем следующий номер из очереди */   
                       if(target==null)  break ;

                    Message("Call executed: "+target) ;

                           Stations[n].Target     = target ;
//                         Stations[n].NextAttempt=(DateTime.Now).AddSeconds(HANGUP_PAUSE) ;
                           Stations[n].Quota.Calls_Crn_Trip++ ;

                               proc                          =new Process() ;
                               proc.StartInfo.FileName       =Environment.CommandLine.Substring(0, Environment.CommandLine.IndexOf(' '));
                               proc.StartInfo.FileName       =proc.StartInfo.FileName.Trim('\"');
                               proc.StartInfo.FileName       =proc.StartInfo.FileName.Replace(".vshost", "");
                               proc.StartInfo.Arguments      ="Call "+CfgPath+" "+Stations[n].Extension+" "+target ;
                               proc.StartInfo.UseShellExecute= false ;

                           try 
                           {
                               proc.Start() ;
                           }
                           catch (Exception exc)
                           {
                              Message("Call processor start error: "+exc.Message) ;
                           }

                               proc=null ;

                             break ;
                       } 
/*---------------------------------------------------------- Цикл сканирования */

        } while(ScanCompleted==0) ;

/*------------------------------------------------------- Главный рабочий цикл */

      } while(false) ;                                                        /* BLOCK MAIN */

/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");

}

private static void iCall_CallDetection(object source, FileSystemEventArgs e)
{
  if(e.ChangeType==WatcherChangeTypes.Created) {

          Message("Call request detected: "+e.Name) ;
   TargetsControl("Add", e.Name.Substring(0, e.Name.IndexOf('.'))) ;
                                               }
  else                                         { 

          Message("Call acknowledge detected: "+e.Name) ;
   TargetsControl("Delete", e.Name.Substring(0, e.Name.IndexOf('.'))) ;
                                               }
}

private static void iCall_CtrlDetection(object source, FileSystemEventArgs e)
{
  string  extension ;


             extension=e.Name.Substring(0, e.Name.IndexOf('.')) ;

          Message("Control request detected: "+e.Name) ;

   if(String.Compare(extension, "Stop",  true)==0)
   {
                    ScanCompleted=1 ;   
   }
   else
   if(e.Name.IndexOf(".err")>=0)
   {
        foreach(Station phone in Stations)
          if(String.Compare(phone.Extension, extension,  true)==0) {
                  phone.Quota.Calls_Crn_Trip-- ;
              TargetsControl("Recall", phone.Target) ;
                                       phone.Target=null ;
                                                                   }
   }
   else
   if(e.Name.IndexOf(".rel")>=0)
   {
        foreach(Station phone in Stations)
          if(String.Compare(phone.Extension, extension,  true)==0) {
                                       phone.Target=null ;
                                                                   }
   }

        Thread.Sleep (100) ;
          File.Delete(e.FullPath) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Ветвь отработки одиночного звонока                          */

static void SingleCall(string  extension, string  target)

{
          DMCC_this  iface ;
            Station  phone ;
   DMCC_phone_times  times ;
  DMCC_phone_events  events ;
           DateTime  current_time ;
            Boolean  error ;
            Boolean  done ;           /* Флаг установления соединения */
                int  status ;

/*----------------------------------------------------------------- Подготовка */

                TraceOnly=0 ;

              Log.MaxSize=0 ;
  
/*---------------------------------------- Идентификация виртуального телефона */

                                                                phone=null ;

   foreach(Station phone_ in Stations)
     if(String.Compare(phone_.Extension, extension,  true)==0)  phone=phone_ ;

     if(phone==null) {
                        Message("SingleCall - ERROR unknown extension: "+extension) ;
              iCall_ErrorSignal( extension, "Unknown extension") ;            /* Создаем сигнал отключения телефона */                                                 
                                     return ;
                     }
/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="SingleCall" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("SingleCall - Connecting...") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           Message("SingleCall - ERROR - Connect:\r\n " + iface.Error + "\r\n") ;
                 iCall_ErrorSignal( extension, "Connect: "+iface.Error) ;     /* Создаем сигнал отключения телефона */                                                 
                              return ;
                        }

                          }
/*--------------------------------------------- Создание виртуального телефона */

                         Message("SingleCall - Create Phone...") ;

                             error=false ;
                              done=false ;
                             times=new DMCC_phone_times() ;
                            events=new DMCC_phone_events() ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               Message("SingleCall - ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                                 "Extension :" + phone.Extension  + "\r\n" +
                                                                 "Password  :" + phone.Password   + "\r\n" +
                                                                 "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                                 "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                            }
                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {
                              phone.Phone=phone.Idx ;
                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Обработка ошибок */
     if(error) {
                 iCall_ErrorSignal(extension, "CreatePhone: "+iface.Error) ;  /* Создаем сигнал отключения телефона */                                                 
               }
     else      {
                           Message("SingleCall - SUCCESS Create Phones") ;
               }  
/*----------------------------------------------------------- Инициация звонка */

     if(phone.Phone>=0) {

       if(Simulation==0) {

              phone.Link=iface.MakeCall(phone.Phone, target) ;                /* Инициализируем соединение */
           if(phone.Link==null) {                                             /* Если ошибка... */

                    Message(phone.Phone + " error") ;
                    Message(iface.Error) ;

                                        phone.Status="Error" ;
                                        phone.Error ="Make Call: " + iface.Error ;
                    WriteScanStatistics(phone) ;
                                }
           else                 {
                                     phone.Status    ="Normal" ;
                                }
                         }
       else              {
                                    phone.CallStatus="MonitorStopped" ;
                                    phone.Status    ="Normal" ;
                                      Thread.Sleep(1000) ;
                         }

                        }
/*-------------------------------------------- Цикл ожидания завершения звонка */
 
   do {                                                                       /* LOOP */
                  Thread.Sleep(1000) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - Определение статуса звонка */
                              current_time=DateTime.Now ;

    do {
         if(Simulation==1) {                                                  /* Эмуляция звонка */
                              times.InitTime     =current_time ;
                              times.DeliveryTime =current_time ;
                              times.ConnectTime  =current_time ;
                              times.ClearTime    =current_time ;
                             events.Established  =true ;
                              phone.Delivery_time=  5 ;
                              phone.Connect_time =  5 ;
                              phone.CallStatus   ="ReadyForNext" ;
                                       break ;
                           }

              phone.CallStatus=iface.GetCallStatus(phone.Phone, ref times, ref events) ;

       if(phone.Status!="Normal")  break ;

                        phone.Delivery_time=0 ;
                        phone.Connect_time =0 ;
                        phone.Active_time  =0 ;

       if(times.DeliveryTime!=DateTime.MinValue)   phone.Delivery_time=(int)(times.DeliveryTime-times.InitTime).TotalSeconds ;
       else                                      {
                                                   phone.Delivery_time=(int)(      current_time-times.InitTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ConnectTime !=DateTime.MinValue)   phone.Connect_time =(int)(times.ConnectTime-times.DeliveryTime).TotalSeconds ;
       else                                      {
                                                   phone.Connect_time =(int)(     current_time-times.DeliveryTime).TotalSeconds ;
                                                            break ;
                                                 }

       if(times.ClearTime   !=DateTime.MinValue)   phone.Active_time  =(int)(times.ClearTime-times.ConnectTime).TotalSeconds ;
       else                                        phone.Active_time  =(int)(   current_time-times.ConnectTime).TotalSeconds ;

     } while(false) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - -  Установление соединения */
    if(phone.Status=="Normal")
     if(events.Established==true) {

          if(!done)  File.Delete(CallsFolder+"\\"+target+".call") ;           /* Удаляем флаг-файл заказа номера */
              done=true ;
                                  }
/*- - - - - - - - - - - - - - - - -  Контроль длительности до входящего звонка */
    if(phone.Status=="Normal")
     if(times.DeliveryTime==DateTime.MinValue)
      if(phone.Delivery_time>DropDelivery) {

                          Message(phone.Phone + " dropped by time (not deliveried)") ;

                                              phone.Status="Offline" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                           }
/*- - - - - - - - - - - - - - - - - - - Контроль длительности входящего звонка */
    if(phone.Status=="Normal")
     if(times.ConnectTime==DateTime.MinValue)
      if(phone.Connect_time>DropConnect) {

                          Message(phone.Phone + " dropped by time (not connected)") ;

                                              phone.Status="Ignored" ;
                               iface.DropCall(phone.Phone) ;

                                                 continue ;
                                         }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение звонка */
     if(phone.CallStatus=="ReadyForNext") {

                          Message(phone.Phone + " completed") ;

                        phone.Quota.Calls_Crn_Trip ++ ;
                        phone.Quota.Times_Crn_Trip +=phone.Active_time ;
                        phone.Quota.Calls_Crn_Total++ ;
                        phone.Quota.Times_Crn_Total+=phone.Active_time ;

                                             WriteScanStatistics(phone) ;
                                                                 phone.Target=null ;

                                                    break ;
                                          } 
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
      } while(true) ;                                                         /* END LOOP */

/*-------------------------------------------- Отлючение виртуального телефона */

     if(phone.Phone>=0) {

       if(Simulation==0) {
                             iface.DeletePhone(phone.Phone) ;
                         }

       if(events.Established==true)  iCall_ReleaseSignal(extension) ;         /* Создаем сигнал освобождения телефона */
       else                          iCall_ErrorSignal  (extension, phone.Status) ;
                        }
/*------------------------------------------------------ Отключение от сервера */

              Message("SingleCall - Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("SingleCall - ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }
/*----------------------------------------------------------------- Завершение */

                TraceOnly=1 ;

/*-----------------------------------------------------------------------------*/

}

private static void iCall_ErrorSignal(string extension, string text)
{
   File.WriteAllText(ControlFolder+"\\"+extension+".err", text) ;
}

private static void iCall_ReleaseSignal(string extension)
{
   File.WriteAllText(ControlFolder+"\\"+extension+".rel", extension) ;
}
/*******************************************************************************/
/*                                                                             */
/*                    Ветвь монторинга флаг-файлов                             */

static void Flag()

{
          DMCC_this   iface ;
  FileSystemWatcher   ctrl_watcher ;
            Boolean   error ;
                int   status ;
           DateTime   time ;
     ConsoleKeyInfo   chr ;
            Boolean   event_flag ;
                int   target_id ;
                int   station_id ;
            Process   proc ;
             string   value ;
             string[] words ;
                int   priority ;
                int   n ;
                int   i ;

/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations     ==null) {
                              MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                                return ;
                            }
    if(DropDelivery ==   0) {
                              MessageWait("ERROR Configuration - <DropDelivery> specificator is missed") ;
                                return ;
                            }
    if(DropConnect  ==   0) {
                              MessageWait("ERROR Configuration - <DropConnect> specificator is missed") ;
                                return ;
                            }
    if(FlagsSpecPath==null) {
                              MessageWait("ERROR Configuration - <FlagsSpecPath> specificator is missed") ;
                                return ;
                            }
    if(ControlFolder==null) {
                              MessageWait("ERROR Configuration - <ControlFolder> specificator is missed") ;
                                return ;
                            }

    if(FlagFileActions_cnt==0) {
                              MessageWait("ERROR Configuration - No one Flag-File specified") ;
                                   return ;
                               }
/*----------------------------------------------------------------- Подготовка */
 
                             Message("FLAG mode\r\n") ;
          if(Simulation==1)  Message("SIMULATION mode\r\n") ;

/*------------------------------------ Проверка создания виртуальных телефонов */

                             error=false ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Соединение с сервером */
             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Flag" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

        if(Simulation==0) {

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }

                          }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Боевой режим */
     if(Simulation==0) {

                         Message("Create Phones...") ;

        foreach(Station phone in Stations) {

             phone.Phone=iface.CreatePhone(phone.Extension, Avaya_SwitchName, Avaya_SwitchIP, phone.Password) ;
          if(phone.Phone<0) {
                               MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n" +
                                                   "Extension :" + phone.Extension  + "\r\n" +
                                                   "Password  :" + phone.Password   + "\r\n" +
                                                   "SwitchName:" + Avaya_SwitchName + "\r\n" +
                                                   "SwitchIP  :" + Avaya_SwitchIP   + "\r\n"   ) ;
                               error=true ;
                                  break ;
                            }
                                           }

                         Message("Delete Phones...") ;

        foreach(Station phone in Stations)
          if(phone.Phone>=0)  iface.DeletePhone(phone.Phone) ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Режим эмуляции */
     else              {

        foreach(Station phone in Stations)  phone.Phone=phone.Idx ;

                       }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Отключение от сервера */
              Message("Disconnecting...") ;

        if(Simulation==0) {

             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n") ;
                        }

                          }

          if(error)  return ;

               MessageWait("SUCCESS Create Phones\r\n") ;

/*-------------------------------------------- Подготовка регистратора событий */

          ctrl_watcher                    =new FileSystemWatcher() ;
          ctrl_watcher.Path               = ControlFolder ;
          ctrl_watcher.Filter             ="*.*"  ;
          ctrl_watcher.NotifyFilter       = NotifyFilters.FileName ;
          ctrl_watcher.Created           += new FileSystemEventHandler(iFlag_CtrlDetection);
          ctrl_watcher.EnableRaisingEvents= true ;

/*--------------------------------------------------- Главный управляющий цикл */

  do {

                  Thread.Sleep(1000) ;

   foreach(FlagFileAction FlagFile in FlagFileActions) {

        if(FlagFile==null)  continue ;

                    time=DateTime.Now ;

/*------------------------------------------------ Обработка консольных команд */

       while(Console.KeyAvailable) {

            chr=Console.ReadKey(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Завершение работы */
         if(chr.KeyChar.Equals('s')) {
                                       AddControl("Urgent", "Stop") ;
                                            ScanCompleted=1 ;
                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - Отображение состояние контроля */
         else
         if(chr.KeyChar.Equals('l')) {

                                Console.Write("\r\nFlag-files\r\n") ;

            for(i=0 ; i<FlagFileActions_cnt ; i++) {
                                Console.Write(FlagFileActions[i].id + " : " +
                                              FlagFileActions[i].event_done + "/" +
                                              FlagFileActions[i].event_mark + "\r\n"
                                             ) ;
                                                   }

                                Console.Write("\r\nActions queue\r\n") ;

            for(i=0 ; i<TargetActions_cnt ; i++) {
                                Console.Write(TargetActions[i].phone + " : " +
                                              TargetActions[i].station + "  " +
                                              TargetActions[i].id + "\r\n"
                                             ) ;
                                                   }

                                Console.Write("\r\nStations pool\r\n") ;

            foreach(Station phone in Stations)
                                Console.Write(phone.Extension + " : " +
                                              phone.Target    + "\r\n"
                                             ) ;


                                Console.Write("\r\n") ;

                                                 break ;
                                     }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
         else                        {
                                Console.Write("\r\nControl keys available:\r\n" +
                                              "  s  -  stop program\r\n" +        
                                              "  l  -  list file-flags and actions queue \r\n"
                                             ) ; 
                                     }
                                   } 

           if(ScanCompleted!=0)  break ;

/*------------------------------------------------------------ Очистка очереди */

        for(i=0, n=0 ; i<TargetActions_cnt ; i++)
          if(TargetActions[i].exclude==false) {

            if(i!=n) {  TargetActions[n].id     =TargetActions[i].id ;
                        TargetActions[n].phone  =TargetActions[i].phone ;
                        TargetActions[n].station=TargetActions[i].station ;
                        TargetActions[n].exclude=TargetActions[i].exclude ;  }

                                      n++ ;
                                              }

                      TargetActions_cnt=n ;

/*--------------------------------------------------------- Выполнение обзвона */

    do {
/*- - - - - - - - - - - - - - - - - - - - - - - - - - Определение цели дозвона */
                            target_id=-1 ;
                             priority=int.MaxValue ;

        for(i=0, n=0 ; i<TargetActions_cnt ; i++)
          if(TargetActions[i].station==null)
           if(TargetActions[i].priority<priority) {  target_id=              i ;
                                                      priority=TargetActions[i].priority ;
                                                                    break ;                 }

          if(target_id<0)  break ;  
/*- - - - - - - - - - - - - - - - - - - - - - -  Определение свободной станции */
                     station_id=-1 ;

          for(i=0 ; i<Stations.Count() ; i++)                                 /* Поиск свободной станции с минимальной загрузкой */
            if(Stations[i].Target==null) {  station_id=i ;
                                                break ;     }

           if(station_id<0)  break ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - Вызов агента дозвона */
                            Message("Call by " + Stations[station_id].Extension + " to " + TargetActions[target_id].phone) ;

              Stations[station_id].Target =TargetActions[target_id].phone ;
         TargetActions[ target_id].station=     Stations[station_id].Extension ;

               proc                          =new Process() ;
               proc.StartInfo.FileName       =Environment.CommandLine.Substring(0, Environment.CommandLine.IndexOf(' '));
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Trim('\"');
               proc.StartInfo.FileName       =proc.StartInfo.FileName.Replace(".vshost", "");
               proc.StartInfo.Arguments      ="Flag "+CfgPath+" "+Stations[station_id].Extension+
                                                            " \"Flag;"+Stations[station_id].Target+
                                                                   ":"+TargetActions[target_id].id+"\"" ;
               proc.StartInfo.UseShellExecute= false ;

          try 
          {
               proc.Start() ;
          }
          catch (Exception exc)
          {
              Message("Call processor start error: "+exc.Message) ;
          }

               proc=null ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
       } while(false) ;

/*-------------------------------------------- Контроль периодичности проверки */

        if(FlagFile.next_check>time)  continue ;

           FlagFile.next_check=DateTime.Now.AddSeconds(FlagFile.check_period) ;

               Message("...") ;
               Message("Check "+FlagFile.id) ;

/*----------------------------------------------- Проверка сигнального события */

                                event_flag=false ;

        if(FlagFile.check_type=="existence") {
                if(File.Exists(FlagFile.check_path)==false)  event_flag=true ;
                                             }
        else
        if(FlagFile.check_type=="absence"  ) {
                if(File.Exists(FlagFile.check_path)==true )  event_flag=true ;
                                             }
        else
        if(FlagFile.check_type=="change"   ) {

            do {
                  if(File.Exists(FlagFile.check_path)==false) {  event_flag=true ;  break ;  }

                  if(File.GetLastWriteTime(FlagFile.check_path)<DateTime.Now.AddSeconds(-FlagFile.check_period)) {  event_flag=true ;  break ;  }

               } while(false) ;
                                             }
        else                                 {
                                                Message("ERROR - Unknown check type: "+FlagFile.check_type) ;
                                                    continue ;
                                             }

        if(event_flag) {
                           Message("Event detected") ;

            if(FlagFile.check_threshold>0)                                    /* Если задана задержка срабатывания... */
             if(FlagFile.event_mark==DateTime.MinValue)
                    FlagFile.event_mark=DateTime.Now.AddSeconds(FlagFile.check_threshold) ;
                    
             if(FlagFile.event_mark>time)  continue ;
                       }
        else           {
                           FlagFile.event_mark=DateTime.MinValue ;
                           FlagFile.event_done= 0 ;

                for(i=0 ; i<TargetActions_cnt ; i++)
                  if(TargetActions[i].id==FlagFile.id)  TargetActions[i].exclude=true ;

                       }
/*---------------------------------------------------- Реагирование на событие */

        if(event_flag && FlagFile.event_done==0) {

                         FlagFile.event_done=1 ;

                           Message("Event raised...") ;

                  value=FlagFile.targets ;
                  words=value.Split(',') ;                                    /* Разбиваем строку на слова */

                     n =TargetActions_cnt ;

           for(i=0 ; i<words.Count() ; i++) {                                 /* По номерам... */

             if(TargetActions[n]==null)  TargetActions[n]=new TargetAction() ;

                TargetActions[n].id         =FlagFile.id ;
                TargetActions[n].phone      =  words[i] ;
                TargetActions[n].linked_type=FlagFile.alert_type ;
                TargetActions[n].attempts   =FlagFile.alert_attempts ;
                TargetActions[n].station    =  null ;
                TargetActions[n].exclude    =  false ;
                TargetActions[n].priority   =    0  ;
                              n++ ;
                                            }

                TargetActions_cnt=n ;
                                                 }

/*--------------------------------------------------- Главный управляющий цикл */

                                                       } 
     } while(ScanCompleted==0) ;

/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");
}

private static void iFlag_CtrlDetection(object source, FileSystemEventArgs e)
{
  string  extension ;
  string  text ;
     int  n ;
     int  i ;


             extension=e.Name.Substring(0, e.Name.IndexOf('.')) ;

          Message("Control request detected: "+e.Name) ;

        for(n=0 ; n<TargetActions_cnt ; n++)
          if(TargetActions[n].station==extension)  break ;

   if(n>=TargetActions_cnt)
   {
        Message("ERROR - Unknown extension callback: "+extension) ;
   }
   else
   if(String.Compare(extension, "Stop",  true)==0)
   {
                    ScanCompleted=1 ;   
   }
   else
   if(e.Name.IndexOf(".err")>=0)
   {
        TargetActions[n].station =null ;
        TargetActions[n].priority++   ;
   }
   else
   if(e.Name.IndexOf(".rel")>=0)
   {
               Thread.Sleep (100) ;
            text=File.ReadAllText(e.FullPath) ;

                                             TargetActions[n].station =null ;
                                             TargetActions[n].priority++   ;

        if(text.IndexOf(";")>=0) {
                                             TargetActions[n].exclude=true ;                                                 

            for(i=0 ; i<TargetActions_cnt ; i++)                              /* Отработка групповых правил */
             if(              i    !=              n    &&
                TargetActions[i].id==TargetActions[n].id  )
              if(TargetActions[n].linked_type=="any")  TargetActions[i].exclude=true ;

                                 }
        else                     {
                                             TargetActions[n].attempts-- ;
           if(TargetActions[n].attempts<=0)  TargetActions[n].exclude=true ;                                                 
                                 }
   }

        foreach(Station phone in Stations)                                    /* Освобождение станции */
          if(String.Compare(phone.Extension, extension,  true)==0) {
                                       phone.Target=null ;
                                                                   }

        Thread.Sleep (100) ;
          File.Delete(e.FullPath) ;                                           /* Удаление флаг-файла */
}
/*******************************************************************************/
/*                                                                             */
/*                                 Тестовая ветвь                              */

static void Test()

{
        DMCC_this  iface ;
              int  status ;
              int  phone_1 ;
           string  link_1 ;
           string  call_1 ;
              int  call_stage ;
              int  n ;

/*----------------------------------------------------------------- Подготовка */

                          Message("TEST mode\r\n") ;

/*---------------------------------------------- Проверка полноты конфигурации */

    if(Stations   ==null) {
                            MessageWait("ERROR Configuration - <Stations> specificator is missed") ;
                              return ;
                          }
    if(ScanNumbers==null) {
                            MessageWait("ERROR Configuration - <ScanNumbers> specificator is missed") ;
                              return ;
                          }
/*------------------------------------------------------ Соединение с сервером */

             iface             = new DMCC_this() ;
             iface.ServiceIP   =Avaya_ServiceIP ;
             iface.ServicePort =Avaya_ServicePort ;
             iface.Application ="Test" ;
             iface.UserName    =Avaya_UserName ;
             iface.UserPassword=Avaya_UserPassword ;

                          Message("Connecting...\r\n") ;

             status=iface.Connect() ;
          if(status!=0) {
                           MessageWait("ERROR Connect:\r\n " + iface.Error + "\r\n") ;
                             return ;
                        }
/*------------------------------------------------------- Главный рабочий цикл */

   do {                                                                       /* BLOCK MAIN */

/*--------------------------------------------- Создание виртуального телефона */

             phone_1=iface.CreatePhone(Stations[0].Extension, Avaya_SwitchName, Avaya_SwitchIP, Stations[0].Password) ;
          if(phone_1<0) {
                           MessageWait("ERROR Create Phone:\r\n " + iface.Error + "\r\n") ;
                              break ;
                        }

                           MessageWait("SUCCESS Create Phone\r\n");

/*--------------------------------------------------------------- Блок дозвона */

     for(n=0 ; n<1 ; n++) {

             link_1=iface.MakeCall(phone_1, ScanNumbers) ;
//           link_1=iface.MakeCall(phone_1, "989269060031") ;
          if(link_1==null) {
                             MessageWait("ERROR Make Call:\r\n " + iface.Error + "\r\n");
                                break ;
                           }

              Message("");

                              call_stage=0 ;

            while(true) {
                             call_1=iface.GetCallStatus(phone_1) ;                                
                          if(call_1=="ReadyForNext") {
                                                        break ;
                                                     } 
                          else
                          if(call_1=="Established" ) {

                                  if(call_stage==0)  iface.DigitsToCall(phone_1, "1234567890", true) ;
                                     call_stage++ ; 
                                                     }
                          else                       {
//                                                     Message("CALL status:" + call_1 + "             \r") ;
                                                        Thread.Sleep(100) ;
                                                     }
                        } 

                          } 

              MessageWait("Calling completed") ;

/*--------------------------------------------- Удаление виртуального телефона */

                         Message("Delete Phone...");
               iface.DeletePhone(phone_1);

/*------------------------------------------------------- Главный рабочий цикл */

      } while(false) ;                                                        /* BLOCK MAIN */

/*------------------------------------------------------ Отключение от сервера */

              Message("Disconnecting...");
             status=iface.Disconnect() ;
          if(status!=0) {
              Message("ERROR Disconnect:\r\n " + iface.Error + "\r\n");
                        }
/*-----------------------------------------------------------------------------*/

          MessageWait("\r\nDone!\r\n");

}
/*******************************************************************************/
/*                                                                             */
/*                               Вывод текстового сообщения                    */

  static  void  Trace(string  text)
{
    text=DateTime.Now.TimeOfDay.ToString().Substring(0,11)+"  "+text;

          Console.WriteLine(text);
              Log.WriteLine(text) ;
}

  static  void  Message(string  text)
{
   if(TraceOnly!=0)  return ;

    text=DateTime.Now.TimeOfDay.ToString().Substring(0,11)+"  "+text;

          Console.WriteLine(text) ;
              Log.WriteLine(text) ;
}

  static  void  MessageWait(string  text)
{
   if(TraceOnly!=0)  return ;

    text=DateTime.Now.TimeOfDay.ToString().Substring(0,11)+"  "+text;

          Console.WriteLine(text) ;
              Log.WriteLine(text) ;

   if(WaitUser      !=0 ||                                 
      WaitUserStrong!=0   ) {

          Console.Write("\r\n Press Enter...");
          Console.ReadLine();
          Console.WriteLine(" ...Continued");
                            }
}

/*******************************************************************************/
/*                                                                             */
/*                       Считывание конгигурационного файла                    */

  static  int  ReadConfig(string  path)
{
  StreamReader    file ;
        string    text ;
        string    prefix ;
        string    key ;
        string    value ;
        string[]  words ;
        string[]  sub_words ;
          bool    done_flag ;
           int    pos ;
           int    n ;
           int    i ;

/*-------------------------------------------------------- Установка умолчаний */

                        AgentPeriod=1800 ;
                     GeneratePeriod=  60 ;
               GroupsGeneratePeriod=24*60 ;

/*------------------------------------------------------------- Открытие файла */

   try 
   {
               file= new StreamReader(path) ;
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - configuration file open error:\r\n"+exc.Message) ;
                          return(-1) ;
   }
/*----------------------------------------------------------- Считывание файла */

     do {
             text=file.ReadLine() ;
          if(text==null)  break ;

             text=text.Replace('\t', ' ') ;
             text=text.Trim() ;

          if(text               =="" )  continue ;
          if(text.Substring(0,1)==";")  continue ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Выделение ключа */
             pos=text.IndexOf("=") ;
          if(pos<0) {
                       MessageWait("ERROR - invalid line structure in configuration file:\r\n" + text) ;
                            return(-1) ;
                    }

             prefix=text.Substring(0, pos) ;
             prefix=prefix.Trim() ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - Обработка значения */
                     key="Simulation" ;
          if(prefix==key) {  Simulation=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="ClearFlag" ;
          if(prefix==key) {  ClearFlag=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="ActiveTime" ;
          if(prefix==key) {  ActiveTime=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="AgentPeriod" ;
          if(prefix==key) {  AgentPeriod=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="GeneratePeriod" ;
          if(prefix==key) {  GeneratePeriod=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="ScanGroupsPeriod" ;
          if(prefix==key) {  GroupsGeneratePeriod=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="QuotaPath" ;
          if(prefix==key) {  QuotaPath=text.Substring(pos+1) ; continue ;  }

                     key="QuotaCycle" ;
          if(prefix==key) {  QuotaCycle=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="CalendarPath" ;
          if(prefix==key) {  CalendarPath=text.Substring(pos+1) ; continue ;  }

                     key="CalendarCycle" ;
          if(prefix==key) {  CalendarCycle=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="Avaya_ServiceIP" ;
          if(prefix==key) {  Avaya_ServiceIP=text.Substring(pos+1) ; continue ;  }

                     key="Avaya_ServicePort" ;
          if(prefix==key) {  Avaya_ServicePort=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="Avaya_UserName" ;
          if(prefix==key) {  Avaya_UserName=text.Substring(pos+1) ; continue ;  }

                     key="Avaya_UserPassword" ;
          if(prefix==key) {  Avaya_UserPassword=text.Substring(pos+1) ; continue ;  }

                     key="Avaya_SwitchName" ;
          if(prefix==key) {  Avaya_SwitchName=text.Substring(pos+1) ; continue ;  }

                     key="Avaya_SwitchIP" ;
          if(prefix==key) {  Avaya_SwitchIP=text.Substring(pos+1) ; continue ;  }

                     key="ScanType" ;
          if(prefix==key) {  ScanType=text.Substring(pos+1) ; continue ;  }

                     key="ScanPrefix" ;
          if(prefix==key) {  ScanPrefix=text.Substring(pos+1) ; continue ;  }

                     key="ScanNumbers" ;
          if(prefix==key) {  ScanNumbers+=text.Substring(pos+1)+"," ; continue ;  }

                     key="ScanGroups" ;
          if(prefix==key) {  TargetsGroups_list+=text.Substring(pos+1)+";" ; continue ;  }

                     key="ScanAllias" ;
          if(prefix==key) {  TargetsAllias_list+=text.Substring(pos+1)+";" ; continue ;  }

                     key="DropDelivery" ;
          if(prefix==key) {  DropDelivery=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="DropConnect" ;
          if(prefix==key) {  DropConnect=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="DropActive" ;
          if(prefix==key) {  DropActive=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="RobotConnect" ;
          if(prefix==key) {  RobotConnect=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="RobotActive" ;
          if(prefix==key) {  RobotActive=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="RandomActive" ;
          if(prefix==key) {  RandomActive=text.Substring(pos+1) ; continue ;  }

                     key="PulseActive" ;
          if(prefix==key) {  PulseActive=Convert.ToDouble(text.Substring(pos+1)) ; continue ;  }

                     key="StatisticsPath" ;
          if(prefix==key) {  StatisticsPath=text.Substring(pos+1) ; continue ;  }

                     key="StatisticsHeader" ;
          if(prefix==key) {  StatisticsHeader=text.Substring(pos+1) ; continue ;  }

                     key="ScanRobotsPath" ;
          if(prefix==key) {  ScanRobotsPath=text.Substring(pos+1) ; continue ;  }

                     key="WaitUser" ;
          if(prefix==key) {  WaitUser=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }

                     key="Stations" ;
          if(prefix==key) {  Stations_list+=text.Substring(pos+1)+"," ; continue ;  }

                     key="CallsFolder" ;
          if(prefix==key) {  CallsFolder=text.Substring(pos+1) ; continue ;  }

                     key="ControlFolder" ;
          if(prefix==key) {  ControlFolder=text.Substring(pos+1) ; continue ;  }
                           
                     key="ReCallSpec" ;
          if(prefix==key) {  ReCallSpec=text.Substring(pos+1) ; continue ;  }
                           
                     key="LogPath" ;
          if(prefix==key) {  Log.Path=text.Substring(pos+1) ; continue ;  }

                     key="LogMaxSize" ;
          if(prefix==key) {  Log.MaxSize=Convert.ToInt32(text.Substring(pos+1)) ; continue ;  }
                           
                     key="TargetsPath" ;
          if(prefix==key) {  TargetsPath=text.Substring(pos+1) ; continue ;  }

                     key="ResultsPath" ;
          if(prefix==key) {  ResultsPath=text.Substring(pos+1) ; continue ;  }

                     key="RangesPath" ;
          if(prefix==key) {  RangesPath=text.Substring(pos+1) ; continue ;  }

                     key="RangesPrefix" ;
          if(prefix==key) {  RangesPrefix=text.Substring(pos+1) ; continue ;  }

                     key="TalkFile" ;
          if(prefix==key) {  TalkFile=text.Substring(pos+1) ; continue ;  }

                     key="FlagsSpecPath" ;
          if(prefix==key) {  FlagsSpecPath=text.Substring(pos+1) ; continue ;  }

                     key="TonesAction" ;
          if(prefix==key) {
                  value=text.Substring(pos+1) ;
                  words=value.Split(':') ;                                    /* Разбиваем строку на слова */
               if(words==null) {
                                   MessageWait("ERROR - data missing for <TonesAction> in configuration file:\r\n" + text) ;
                                         return(-1) ;
                               } 

                                            n =TonesActions_cnt ;
                               TonesActions[n]=new TonesAction() ;

              for(i=0 ; i<words.Count() ; i++)                                /* Разбираем спецификацию тонового набора */
                     if(i==0)  TonesActions[n].tones  =                words[i] ;
                else if(i==1)  TonesActions[n].action =                words[i] ;
                else if(i==2)  TonesActions[n].use_max=Convert.ToInt32(words[i]) ;

                     if(TonesActions[n].action                == null   ) {   /* Разбор "действия" */
                                 MessageWait("ERROR - action is missed for <TonesAction> in configuration file:\r\n" + text) ;
                                                                                 return(-1) ;
                                                                          }
                     else
                     if(TonesActions[n].action.Substring(0, 5)=="call " ) {
                                    TonesActions[n].target=TonesActions[n].action.Substring(5) ;
                                    TonesActions[n].action="call" ;
                                                                          }
                     else
                     if(TonesActions[n].action.Substring(0, 6)=="write ") {
                                    TonesActions[n].target=TonesActions[n].action.Substring(6) ;
                                    TonesActions[n].action="write" ;
                                                                          }
                     else                                                 {
                                 MessageWait("ERROR - unknown action for <TonesAction> in configuration file:\r\n" + text) ;
                                                                                 return(-1) ;
                                                                          }
                     
                                            TonesActions_cnt++ ; 
                                                     continue ;
                          }

          MessageWait("ERROR - unknown key in configuration file:\r\n" + text) ;
                          return(-1) ;
          
        } while(true) ;

/*---------------------------------------------------- Разборка списка станций */

                Stations_list=Stations_list.Trim(',') ;

          words=Stations_list.Split(',') ;                                    /* Разбиваем строку на слова */
       if(words==null) {
                           MessageWait("ERROR - data missing for <Stations> in configuration file:\r\n" + text) ;
                             return(-1) ;
                       } 

           Stations=new Station[words.Count()] ;                              /* Выделяем массив описания станций */

      for(i=0 ; i<words.Count() ; i++) {                                      /* Разбираем параметры станций */
 
            value=words[i].Trim() ;
              pos=value.IndexOf('/') ;
           if(pos<0) {
                        MessageWait("ERROR - missing /-selector in data for <Stations> in configuration file:\r\n" + text) ;
                          return(-1) ;
                     }

           Stations[i]          =new Station() ;
           Stations[i].Extension=value.Substring(0, pos) ;
           Stations[i].Password =value.Substring(pos+1) ;
           Stations[i].Idx      =  i ;
                                       }
/*----------------------------------- Разборка списков групп и алиасов номеров */
/*- - - - - - - - - - - - - - - - - - - - - - -  Разборка списка групп номеров */
                    TargetsGroups    =new TargetGroup[TARGETS_AG_MAX+1] ;     /* Выделяем массив описания групп номеров */
                    TargetsGroups_cnt= 0 ;

  if(TargetsGroups_list!=null)
    do {
                TargetsGroups_list=TargetsGroups_list.Trim(';') ;

          words=TargetsGroups_list.Split(';') ;                               /* Разбиваем строку на слова */
       if(words==null) break ;

      for(i=0 ; i<words.Count() ; i++) {                                      /* Разбираем параметры групп номеров */

        if(i>=TARGETS_AG_MAX) {
                 MessageWait("ERROR - too many <ScanGroups> parameters in configuration file (max 100)") ;
                                        return(-1) ;
                              }
 
               value=words[i].Trim() ;

           sub_words=value.Split(new Char[] {'/',':'}) ;                      /* Разбиваем спецификацию на параметры */
        if(sub_words.Count()!=4) {
                 MessageWait("ERROR - illegal value for <ScanGroups> in configuration file: " + value) ;
                                        return(-1) ;
                                 }

        if(sub_words[2]!="Single" && 
           sub_words[2]!="Heap"     ) {
                             MessageWait("ERROR - Unknown ScanType in <ScanGroups> in configuration file: " + value) ;
                                              return(-1) ;
                                      }

        if(sub_words[1]=="")  sub_words[1]="0" ;

           TargetsGroups[i+1]         =new TargetGroup() ;
           TargetsGroups[i+1].Station =                sub_words[0] ;
           TargetsGroups[i+1].Size    =Convert.ToInt32(sub_words[1]) ;
           TargetsGroups[i+1].ScanType=                sub_words[2] ;
           TargetsGroups[i+1].Targets =                sub_words[3] ;
                                       }

            TargetsGroups_cnt=words.Count() ;

       } while(false) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - Разборка алиасов номеров */
                    TargetsAllias    =new TargetAllias[2*TARGETS_AG_MAX+1] ;  /* Выделяем массив описания алиасов номеров */
                    TargetsAllias_cnt= 0 ;

  if(TargetsAllias_list!=null)
    do {
                TargetsAllias_list=TargetsAllias_list.Trim(';') ;

          words=TargetsAllias_list.Split(';') ;                               /* Разбиваем строку на слова */
       if(words==null) break ;

      for(i=0 ; i<words.Count() ; i++) {                                      /* Разбираем параметры алиасов номеров */

        if(i>=TARGETS_AG_MAX) {
                 MessageWait("ERROR - too many <ScanAllias> parameters in configuration file (max 100)") ;
                                        return(-1) ;
                              }
 
               value=words[i].Trim() ;

           sub_words=value.Split(new Char[] {'/',':'}) ;                      /* Разбиваем спецификацию на параметры */
        if(sub_words.Count()!=4) {
                 MessageWait("ERROR - illegal value for <ScanAllias> in configuration file: " + value) ;
                                        return(-1) ;
                                 }

           TargetsAllias[i+1]         =new TargetAllias() ;
           TargetsAllias[i+1].Station =                sub_words[0] ;
           TargetsAllias[i+1].Size    =Convert.ToInt32(sub_words[1]) ;
           TargetsAllias[i+1].Group   =                sub_words[2] ;
           TargetsAllias[i+1].Prefix  =                sub_words[3] ;
                                       }

            TargetsAllias_cnt=words.Count() ;

       } while(false) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Проверка алиасов */
     for(n=1 ; n<=TargetsAllias_cnt ; n++) {
                                                         done_flag=false ;
        foreach(Station phone in Stations)
          if(phone.Extension==TargetsAllias[n].Station)  done_flag=true ;

          if(done_flag==false) {
                 MessageWait("ERROR - unknown Station specified in <ScanAllias> in configuration file: " + TargetsAllias[n].Station) ;
                                   return(-1) ;
                               }

                                                                done_flag=false ;
        for(i=1 ; i<=TargetsGroups_cnt ; i++)
          if(TargetsGroups[i].Station==TargetsAllias[n].Group)  done_flag=true ;

          if(done_flag==false) {
                 MessageWait("ERROR - unknown Group specified in <ScanAllias> in configuration file: " + TargetsAllias[n].Group) ;
                                   return(-1) ;
                               }
                                           }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Проверка групп */
     for(n=1 ; n<=TargetsGroups_cnt ; n++) {

                                                done_flag=false ;

        if(TargetsGroups[n].Station.Substring(0,1)=="G")
        {
          for(i=1 ; i<=TargetsAllias_cnt ; i++)
            if(TargetsAllias[i].Group==TargetsGroups[n].Station)  done_flag=true ;
        }
        else
        {
          foreach(Station phone in Stations)
            if(phone.Extension==TargetsGroups[n].Station)  done_flag=true ;
        }

        if(done_flag==false) {
                 MessageWait("ERROR - unknown Station or group specified in <ScanGroups> in configuration file: " + TargetsGroups[n].Station) ;
                                 return(-1) ;
                             }

        if(TargetsGroups[n].Station.Substring(0,1)=="G")
         if(TargetsGroups[n].Targets.IndexOf("A")==-1) {
                 MessageWait("ERROR - allias placeholder missed in <ScanGroups> in configuration file: " + TargetsGroups[n].Targets) ;
                                 return(-1) ;
                                                       } 
                                           }
/*- - - - - - - - - - - - - - - - - - - - - - - -  Приведение групп по алиасам */
     for(n=1 ; n<=TargetsGroups_cnt ; n++)
        if(TargetsGroups[n].Station.Substring(0,1)=="G")
        {
                        TargetsGroups[n].Size=0 ;

          for(i=1 ; i<=TargetsAllias_cnt ; i++)
            if(TargetsAllias[i].Group==TargetsGroups[n].Station)  TargetsGroups[n].Size+=TargetsAllias[i].Size ;
        }
        else
        {
                         i=TargetsAllias_cnt ;

           TargetsAllias[i+1]         =new TargetAllias() ;
           TargetsAllias[i+1].Station =TargetsGroups[n].Station ;
           TargetsAllias[i+1].Size    =TargetsGroups[n].Size ;
           TargetsAllias[i+1].Group   =TargetsGroups[n].Station ;
           TargetsAllias[i+1].Prefix  ="" ;

           TargetsAllias_cnt++ ;
        }
/*------------------------------------------------------------- Закрытие файла */

               file.Close() ;

/*-----------------------------------------------------------------------------*/

  return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                  Создание шаблона конфигурационного файла                   */

  static  int  ConfigTemplate(string regime, string path)
{
  string  text ;

   try 
   {
       text="" ;

       if(String.Compare(regime, "Test", true)==0)
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Станция в формате номер/пароль\r\n" +
            "ScanNumbers       =Набираемый номер\r\n" ;

       if(String.Compare(regime, "Scan", true)==0) 
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Список станций через запятую в формате номер/пароль (может быть несколько строк)\r\n" +
            "ScanNumbers       =Список диапазонов номеров через запятую (может быть несколько строк)\r\n" +
            ";ScanNumbers       =@Путь к файлу со списком сканируемых номеров\r\n" +
            ";ScanGroups        =Список групп номеров в формате число_номеров/префикс через запятую (может быть несколько строк)\r\n" +
            ";ScanGroupsPeriod  =Период перегенерации групп номеров, минут\r\n" +
            "ControlFolder     =Путь к папке служебных файлов\r\n" +
            "DropDelivery      =Максимальное время ожидания входящего звонка, секунд\r\n" +
            "DropConnect       =Максимальное время ожидания соединения, секунд\r\n" +
            "DropActive        =Максимальное время удержания соединения, секунд\r\n" +
            "RobotConnect      =Определение 'Робота' - максимальное время ожидания соединения, секунд\r\n" +
            "RobotActive       =Определение 'Робота' - минимальное время удержания соединения, секунд\r\n" +
            "RandomActive      =Закон распределения максимального время удержания соединения:Fixed,Uniform\r\n" +
            "StatisticsPath    =Путь к файлу общей статистики, null - не использовать\r\n" +
            "StatisticsHeader  =Заголовок-разделитель для файла общей статистики\r\n" +
            "ScanRobotsPath    =Путь к файлу кандидатов в 'Роботы', null - не использовать\r\n" +
            ";ClearFlag         =Флаг очистки рабочего контекста\r\n" +
            ";AgentPeriod       =Период запуска агента сканирования, секунд\r\n" +
            ";GeneratePeriod    =Период генерации номеров, секунд\r\n" +
            ";PulseActive       =Коэффициент скважности нагрузки, от 0.1 до 0.9\r\n" +
            ";ActiveTime        =Время работы программы, минут\r\n" +
            ";QuotaPath         =Путь к файлу квот, расширение CSV - под MS Excel\r\n" +
            ";QuotaCycle        =Периодичность обновления файла квот, минут\r\n" +
            ";LogPath           =Путь к файлу технологического лога\r\n" +
            ";LogMaxSize        =Максималхный размер файла технологического лога\r\n" +
            ";WaitUser          =Флаг ожидания нажатия клавиш\r\n" ;

       if(String.Compare(regime, "Kick", true)==0) 
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Список станций через запятую в формате номер/пароль (может быть несколько строк)\r\n" +
            "ScanType          =Режим сканирования: Single, Heap\r\n" +
            "ScanPrefix        =Набор фиксированных подстановок сканирования\r\n" +
            "ScanNumbers       =Список диапазонов номеров через запятую (может быть несколько строк)\r\n" +
            ";ScanNumbers       =@Путь к файлу со списком сканируемых номеров\r\n" +
            "ControlFolder     =Путь к папке служебных файлов\r\n" +
            "DropDelivery      =Максимальное время ожидания входящего звонка, секунд\r\n" +
            "StatisticsPath    =Путь к файлу общей статистики, null - не использовать\r\n" +
            "StatisticsHeader  =Заголовок-разделитель для файла общей статистики\r\n" +
            ";AgentPeriod       =Период запуска агента сканирования, секунд\r\n" +
            ";GeneratePeriod    =Период генерации номеров, секунд\r\n" +
            ";ClearFlag         =Флаг очистки рабочего контекста\r\n" +
            ";ActiveTime        =Время работы программы, минут\r\n" +
            ";CalendarPath     =Путь к файлу календаря, расширение CSV - под MS Excel\r\n" +
            ";CalendarCycle     =Периодичность считывания файла календаря, минут\r\n" +
            ";QuotaPath         =Путь к файлу квот, расширение CSV - под MS Excel\r\n" +
            ";QuotaCycle        =Периодичность обновления файла квот, минут\r\n" +
            ";RangesPath        =Путь к файлу таблицы диапазонов номеров\r\n" +
            ";RangesPrefix      =Префикс дозвона\r\n" +
            ";LogPath           =Путь к файлу технологического лога\r\n" +
            ";LogMaxSize        =Максималхный размер файла технологического лога\r\n" +
            ";WaitUser          =Флаг ожидания нажатия клавиш\r\n" ;

       if(String.Compare(regime, "Play", true)==0) 
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Список станций через запятую в формате номер/пароль (может быть несколько строк)\r\n" +
            "TargetsPath       =Путь к файлу обзваниваемых номеров\r\n" +
            "TalkFile          =Файл аудио-приветствия\r\n" +
            "TonesAction       =Список действий по тоновому набору в формате <цифры,*,#>:<номер или действие>:<допустимая нагрузка> (может быть несколько строк)\r\n" +
            "ResultsPath       =Путь к файлу результатов обзвона\r\n" +
            "ControlFolder     =Путь к папке флаг-файлов обработки вызовов\r\n" +
            "DropDelivery      =Максимальное время ожидания входящего звонка, секунд\r\n" +
            "DropConnect       =Максимальное время ожидания соединения, секунд\r\n" +
            "StatisticsPath    =Путь к файлу общей статистики, null - не использовать\r\n" +
            "StatisticsHeader  =Заголовок-разделитель для файла общей статистики\r\n" +
            ";ClearFlag         =Флаг очистки рабочего контекста\r\n" +
            ";QuotaPath         =Путь к файлу квот, расширение CSV - под MS Excel\r\n" +
            ";QuotaCycle        =Периодичность обновления файла квот, минут\r\n" +
            ";LogPath           =Путь к файлу технологического лога\r\n" +
            ";LogMaxSize        =Максималхный размер файла технологического лога\r\n" +
            ";WaitUser          =Флаг ожидания нажатия клавиш\r\n" ;

       if(String.Compare(regime, "Call", true)==0) 
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Список станций через запятую в формате номер/пароль (может быть несколько строк)\r\n" +
            "CallsFolder       =Путь к папке запросов вызовов\r\n" +
            "ControlFolder     =Путь к папке флаг-файлов обработки вызовов\r\n" +
            "ReCallSpec        =Спецификация повторных звонков: <Пауза1>,<Пауза2>,...\r\n" +
            "DropDelivery      =Максимальное время ожидания входящего звонка, секунд\r\n" +
            "DropConnect       =Максимальное время ожидания соединения, секунд\r\n" +
            "ResultsPath       =Путь к файлу результатов обзвона\r\n" +
            "StatisticsPath    =Путь к файлу общей статистики, null - не использовать\r\n" +
            "StatisticsHeader  =Заголовок-разделитель для файла общей статистики\r\n" +
            ";ClearFlag         =Флаг очистки рабочего контекста\r\n" +
            ";QuotaPath         =Путь к файлу квот, расширение CSV - под MS Excel\r\n" +
            ";QuotaCycle        =Периодичность обновления файла квот, минут\r\n" +
            ";LogPath           =Путь к файлу технологического лога\r\n" +
            ";LogMaxSize        =Максималхный размер файла технологического лога\r\n" +
            ";WaitUser          =Флаг ожидания нажатия клавиш\r\n" ;

       if(String.Compare(regime, "Flag", true)==0) 
       text="Avaya_ServiceIP   =IP-адрес сервера AES\r\n" +
            "Avaya_ServicePort =Контактный сервера AES\r\n" +
            "Avaya_UserName    =Имя пользователя\r\n" +
            "Avaya_UserPassword=Пароль пользователя\r\n" +
            "Avaya_SwitchName  =Имя свича\r\n" +
            "Avaya_SwitchIP    =IP-адрес свича\r\n" +
            "Stations          =Список станций через запятую в формате номер/пароль (может быть несколько строк)\r\n" +
            "DropDelivery      =Максимальное время ожидания входящего звонка, секунд\r\n" +
            "DropConnect       =Максимальное время ожидания соединения, секунд\r\n" +
            "FlagsSpecPath     =Путь к файлу описания отслеживаемых флаг-файлов\r\n" +
            "ControlFolder     =Путь к папке флаг-файлов обработки вызовов\r\n" +
            "StatisticsPath    =Путь к файлу общей статистики, null - не использовать\r\n" +
            "StatisticsHeader  =Заголовок-разделитель для файла общей статистики\r\n" +
            ";ClearFlag         =Флаг очистки рабочего контекста\r\n" +
            ";LogPath           =Путь к файлу технологического лога\r\n" +
            ";LogMaxSize        =Максималхный размер файла технологического лога\r\n" +
            ";WaitUser          =Флаг ожидания нажатия клавиш\r\n" ;

             File.AppendAllText(path, text) ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - ConfigTemplate: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                   Считывание файла спецфикации флаг-файлов                  */

  static  int  ReadFileFlagSpecification()
{
  StreamReader   file ;
        string   text ;
        string   prefix ;
        string   key ;
        string   value ;
        string[] words ;
   TonesAction   Action ;
           int   actions_cnt ;
           int   pos ;
           int   n ;
           int   m ;
           int   i ;

/*-------------------------------------------------------------- Инициализация */

                          actions_cnt=0 ;

/*------------------------------------------------------------- Открытие файла */

   try 
   {
               file= new StreamReader(FlagsSpecPath) ;
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - flag-file specification file open error:\r\n"+exc.Message) ;
                          return(-1) ;
   }
/*----------------------------------------------------------- Считывание файла */

                n=-1 ;

     do {
             text=file.ReadLine() ;
          if(text==null)  break ;

             text=text.Replace('\t', ' ') ;
             text=text.Trim() ;

          if(text               =="" )  continue ;
          if(text.Substring(0,1)==";")  continue ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Выделение ключа */
             pos=text.IndexOf("=") ;
          if(pos<0) {
                       MessageWait("ERROR - invalid line structure in configuration file:\r\n" + text) ;
                            return(-1) ;
                    }

             prefix=text.Substring(0, pos) ;
             prefix=prefix.Trim() ;
              value=text.Substring(pos+1) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - Обработка значения */
                     key="Id" ;
          if(prefix==key) {                                                   /* Создание нового слота описания флаг-файла */

            for(i=0 ; i<FlagFileActions_cnt ; i++)
              if(FlagFileActions[i].id==value) {
                       MessageWait("ERROR - duplicated rule id in flag-file specification: " + value) ;
                                                   return(-1) ;
                                               }

                                               n =FlagFileActions_cnt ;
                               FlagFileActions[n]=new FlagFileAction() ;

                               FlagFileActions[n].id            =value ;
                               FlagFileActions[n].alert_type    ="any" ;
                               FlagFileActions[n].alert_attempts= 10  ;

                                          actions_cnt=0 ;
                     
                                            FlagFileActions_cnt++ ; 
                                                     continue ;
                          }

          if(n<0) {                                                           /* Проверка порядка следования ключевых слов в спецификации */
                       MessageWait("ERROR - 'Id' keyword mast be first in file-flag specification") ;
                                                   return(-1) ;
                  }

                     key="CheckPath" ;
          if(prefix==key) {  FlagFileActions[n].check_path=value ; continue ;  }

                     key="CheckType" ;
          if(prefix==key) {  

                if(value!="existence" &&
                   value!="absence"   &&
                   value!="change"      ) {
                       MessageWait("ERROR - unknown 'CheckType' (existence,absence,change) in flag-file specification: " + value) ;
                                                   return(-1) ;
                                          }

                             FlagFileActions[n].check_type=value ;
                                    continue ;  
                          }

                     key="CheckPeriod" ;
          if(prefix==key) {  FlagFileActions[n].check_period=Convert.ToInt32(value) ; continue ;  }

                     key="CheckThreshold" ;
          if(prefix==key) {  FlagFileActions[n].check_threshold=Convert.ToInt32(value) ; continue ;  }

                     key="Targets" ;
          if(prefix==key) {  FlagFileActions[n].targets=value ; continue ;  }

                     key="TalkFile" ;
          if(prefix==key) {  FlagFileActions[n].talk_file=value ; continue ;  }

                     key="AlertTypes" ;
          if(prefix==key) {  

                if(value!="all" &&
                   value!="any"   ) {
                       MessageWait("ERROR - unknown 'AlertTypes' (all,any) in flag-file specification: " + value) ;
                                                   return(-1) ;
                                    }

                             FlagFileActions[n].alert_type=value ; 
                                    continue ;  
                          }

                     key="AlertAttempts" ;
          if(prefix==key) {  FlagFileActions[n].alert_attempts=Convert.ToInt32(value) ; continue ;  }

                     key="TonesAction" ;
          if(prefix==key) {
                  value=text.Substring(pos+1) ;
                  words=value.Split(';') ;                                    /* Разбиваем строку на слова */
               if(words==null) {
                                   MessageWait("ERROR - data missing for <TonesAction> in flag-file specification:\r\n" + text) ;
                                         return(-1) ;
                               } 

                                                         m =actions_cnt ;
                              FlagFileActions[n].actions[m]=new TonesAction() ;
                                                    Action =FlagFileActions[n].actions[m] ;

              for(i=0 ; i<words.Count() ; i++)                                /* Разбираем спецификацию тонового набора */
                     if(i==0)  Action.tones  =                words[i] ;
                else if(i==1)  Action.action =                words[i] ;
                else if(i==2)  Action.use_max=Convert.ToInt32(words[i]) ;

                     if(Action.action                == null     ) {          /* Разбор "действия" */
                                 MessageWait("ERROR - action is missed for <TonesAction> in flag-file specification:\r\n" + text) ;
                                        return(-1) ;
                                                                   }
                     else
                     if(Action.action.Substring(0, 5)=="call "   ) {
                                    Action.target=Action.action.Substring(5) ;
                                    Action.action="call" ;
                                                                   }
                     else
                     if(Action.action.Substring(0, 6)=="write "  ) {
                                    Action.target=Action.action.Substring(6) ;
                                    Action.action="write" ;
                                                                   }
                     else
                     if(Action.action.Substring(0, 8)=="execute ") {
                                    Action.target=Action.action.Substring(8) ;
                                    Action.action="execute" ;
                                                                   }
                     else                                          {
                                 MessageWait("ERROR - unknown action for <TonesAction> in flag-file specification:\r\n" + text) ;
                                       return(-1) ;
                                                                   }

                                                 actions_cnt++ ;
                                                     continue ;
                          }

          MessageWait("ERROR - unknown key in configuration file:\r\n" + text) ;
                          return(-1) ;
          
        } while(true) ;

/*------------------------------------------------------------- Закрытие файла */

               file.Close() ;

/*-----------------------------------------------------------------------------*/

  return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                          Создание шаблона файла квот                        */

  static  int  QuotaTemplate(string path)
{
  string  text ;
     int  pos ;

   try 
   {
               pos=path.LastIndexOf(".") ;

       if(String.Compare(path.Substring(pos), ".CSV", true)==0)
             text="Station;Run Calls;Run Minutes;Executed Calls;Executed Minutes;Trip Calls;Trip Minutes;Total Calls;Total Minutes;Comment\r\n" ;
       else  text="Station\tRun Calls\tRun Minutes\tExecuted Calls\tExecuted Minutes\tTrip Calls\tTrip Minutes\tTotal Calls\tTotal Minutes\tComment\r\n" ;
         
             File.WriteAllText(path, text) ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - QuotaTemplate: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                          Обмен данными с файлом квот                        */

  static  int  QuotaFileCheck(Boolean read_only)
{
        DateTime  file_time ;
          string  arch_path ;
            bool  init_flag ;    /* Флаг инициализации данных */
    StreamReader  file_r ;
    StreamWriter  file_w ;
          string  text ;
            char  delimeter ;
        string[]  words ;
        string[]  data ;
             int  pos ;
             int  grp ;
             int  n  ;
             int  i  ;

/*----------------------------------------------------------- Входной контроль */

    if(               QuotaPath==null            )  return(0) ;
    if(               QuotaPath==""              )  return(0) ;
    if(String.Compare(QuotaPath, "null", true)==0)  return(0) ;

    if(Quotas==null)  Quotas=new Quota[QUOTAS_MAX] ;
    if(Groups==null)  Groups=new Quota[QUOTAS_MAX] ;

                        data=new string[10] ;

    if(File.Exists(QuotaPath)==false) {
          MessageWait("ERROR - quota file is absent: " + QuotaPath) ;
                          return(-1) ;
                                      }
/*--------------------------------------------------- Идентификация типа файла */

               pos=QuotaPath.LastIndexOf(".") ;

       if(String.Compare(QuotaPath.Substring(pos), 
                                      ".CSV", true )==0)  delimeter=';' ;
       else                                               delimeter='\t' ;

/*---------------------------- Помесячная архивация и инициализация файла квот */

                                init_flag=false ;

       file_time=File.GetLastWriteTime(QuotaPath) ;
    if(file_time.Month!=DateTime.Now.Month) {

                arch_path=QuotaPath+"."+file_time.Year.ToString()+"_"+file_time.Month.ToString() ;

      if(File.Exists(arch_path)==false) {
                                            File.Copy(QuotaPath, arch_path, true) ;
                                        }

                                                init_flag=true ;

                                            }
/*----------------------------------------------------------- Считывание файла */

   try 
   {
               file_r= new StreamReader(QuotaPath) ;                            /* Открытие файла */
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - quota file open error for read:\r\n"+exc.Message) ;
                          return(-1) ;
   }

                Quotas_cnt=0 ;

     do {
             text=file_r.ReadLine() ;
          if(text==null)  break ;

          if(Quotas[Quotas_cnt]==null)  Quotas[Quotas_cnt]          =new Quota() ;
                                        Quotas[Quotas_cnt].Row      = text ;
                                        Quotas[Quotas_cnt].processed=false ;
                                               Quotas_cnt++ ;
               
        } while(true) ;

                           file_r.Close() ;                                   /* Закрытие файла */

/*-------------------------------------------------------------- Анализ данных */

        foreach(Station phone in Stations)  phone.Quota.Row=null ;
/*- - - - - - - - - - - - - - - - - - - - - -  Анализ записей станций из файла */
     for(n=0 ; n<Quotas_cnt ; n++) {
 
                  text=Quotas[n].Row+";;;;;;;;;" ;
            words=text.Split(delimeter) ;                                      /* Разбиваем строку на слова */
         if(words==null)  continue ;

       for(i=0 ; i<10 ; i++) {                                                 /* Канонизация набора данных */
              if(i==9)  data[i]= "no remark" ;
              else      data[i]= "0" ;    

              if(words.Count()>i && 
                 words[i]!=""      )  data[i]=words[i] ;
                             }

        foreach(Station phone in Stations)                                    /* Ищем по номеру станции */
         if(String.Compare(phone.Extension, data[0], true )==0) {

                         Quotas[n].processed=true ;

           if(read_only) {
              phone.Quota.Calls_Crn_Trip =Convert.ToInt32(data[1]) ;
              phone.Quota.Times_Crn_Trip =Convert.ToInt32(data[2])*60 ;
              phone.Quota.Calls_Crn_Total=Convert.ToInt32(data[3]) ;
              phone.Quota.Times_Crn_Total=Convert.ToInt32(data[4])*60 ;
                         }

           if(init_flag) {
              phone.Quota.Calls_Crn_Trip =0 ;
              phone.Quota.Times_Crn_Trip =0 ;
              phone.Quota.Calls_Crn_Total=0 ;
              phone.Quota.Times_Crn_Total=0 ;
                         }

              phone.Quota.Calls_Max_Trip =Convert.ToInt32(data[5]) ;
              phone.Quota.Times_Max_Trip =Convert.ToInt32(data[6])*60 ;
              phone.Quota.Calls_Max_Total=Convert.ToInt32(data[7]) ;
              phone.Quota.Times_Max_Total=Convert.ToInt32(data[8])*60 ;
              phone.Quota.Comment        =                data[9] ;
              phone.Quota.Row            =            data[0]
                                          +delimeter+(phone.Quota.Calls_Crn_Trip    ).ToString()
                                          +delimeter+(phone.Quota.Times_Crn_Trip/60 ).ToString()
                                          +delimeter+(phone.Quota.Calls_Crn_Total   ).ToString()
                                          +delimeter+(phone.Quota.Times_Crn_Total/60).ToString()
                                          +delimeter+ data[5]
                                          +delimeter+ data[6]
                                          +delimeter+ data[7]
                                          +delimeter+ data[8]
                                          +delimeter+ data[9] ;
                            Quotas[n].Row=phone.Quota.Row ;
                                                                }

                                   }
/*- - - - - - - - - - - - - - - - - - - - - - -  Анализ записей групп из файла */
     for(n=0 ; n<Quotas_cnt ; n++) {

                  text=Quotas[n].Row+";;;;;;;;;" ;
            words=text.Split(delimeter) ;                                      /* Разбиваем строку на слова */
         if(words==null)  continue ;

       for(i=0 ; i<10 ; i++) {                                                 /* Канонизация набора данных */
              if(i==9)  data[i]= "no remark" ;
              else      data[i]= "0" ;    

              if(words.Count()>i && 
                 words[i]!=""      )  data[i]=words[i] ;
                             }

        foreach(Station phone in Stations)                                    /* Ищем по номеру группы */
         if(String.Compare(phone.Quota.Comment, data[0], true)==0) {

                         Quotas[n].processed=true ;

              grp=GetQuotaGroup(phone.Quota) ;
           if(grp<0) {
                              grp=Groups_cnt ;
                                  Groups_cnt++ ;
                       Groups[grp]=new Quota() ;
                     }

           if(read_only) {
              Groups[grp].Calls_Crn_Trip =Convert.ToInt32(data[1]) ;
              Groups[grp].Times_Crn_Trip =Convert.ToInt32(data[2])*60 ;
              Groups[grp].Calls_Crn_Total=Convert.ToInt32(data[3]) ;
              Groups[grp].Times_Crn_Total=Convert.ToInt32(data[4])*60 ;
                         }

           if(init_flag) {
              Groups[grp].Calls_Crn_Trip =0 ;
              Groups[grp].Times_Crn_Trip =0 ;
              Groups[grp].Calls_Crn_Total=0 ;
              Groups[grp].Times_Crn_Total=0 ;
                         }

              Groups[grp].Calls_Max_Trip =Convert.ToInt32(data[5]) ;
              Groups[grp].Times_Max_Trip =Convert.ToInt32(data[6])*60 ;
              Groups[grp].Calls_Max_Total=Convert.ToInt32(data[7]) ;
              Groups[grp].Times_Max_Total=Convert.ToInt32(data[8])*60 ;
              Groups[grp].Comment        =                data[0] ;
              Groups[grp].Row            =            data[0]
                                          +delimeter+(Groups[grp].Calls_Crn_Trip    ).ToString()
                                          +delimeter+(Groups[grp].Times_Crn_Trip/60 ).ToString()
                                          +delimeter+(Groups[grp].Calls_Crn_Total   ).ToString()
                                          +delimeter+(Groups[grp].Times_Crn_Total/60).ToString()
                                          +delimeter+ data[5]
                                          +delimeter+ data[6]
                                          +delimeter+ data[7]
                                          +delimeter+ data[8]
                                          +delimeter+ data[9] ;
                            Quotas[n].Row=Groups[grp].Row ;

                                                   break ;
                                                                   }

                                   }
/*- - - - - - - - - - - - - - - - -  Инициализация необработанных записей квот */
       if(init_flag) {

     for(n=0 ; n<Quotas_cnt ; n++) 
       if(Quotas[n].processed==false) {

                  text=Quotas[n].Row+";;;;;;;;;" ;
            words=text.Split(delimeter) ;                                      /* Разбиваем строку на слова */
         if(words==null)  continue ;

       for(i=0 ; i<10 ; i++) {                                                 /* Канонизация набора данных */
              if(i==9)  data[i]= "no remark" ;
              else      data[i]= "0" ;    

              if(words.Count()>i && 
                 words[i]!=""      )  data[i]=words[i] ;
                             }

                           Quotas[n].Row=           data[0]
                                         +delimeter+ "0"
                                         +delimeter+ "0"
                                         +delimeter+ "0"
                                         +delimeter+ "0"
                                         +delimeter+data[5]
                                         +delimeter+data[6]
                                         +delimeter+data[7]
                                         +delimeter+data[8]
                                         +delimeter+data[9] ;
                                      } 

                     }
/*- - - - - - - - - - - - - - - - - - - - - - - -  Дополнение записей из файла */
        foreach(Station phone in Stations)  
          if(phone.Quota.Row==null) {

              phone.Quota.Row=            phone.Extension
                              +delimeter+(phone.Quota.Calls_Crn_Trip    ).ToString()
                              +delimeter+(phone.Quota.Times_Crn_Trip/60 ).ToString()
                              +delimeter+(phone.Quota.Calls_Crn_Total   ).ToString()
                              +delimeter+(phone.Quota.Times_Crn_Total/60).ToString()
                              +delimeter+ "0"
                              +delimeter+ "0"
                              +delimeter+ "0"
                              +delimeter+ "0"
                              +delimeter+ "no remark" ;

             if(Quotas[Quotas_cnt]==null)  Quotas[Quotas_cnt]    =new Quota() ;
                                           Quotas[Quotas_cnt].Row=phone.Quota.Row ;
                                                  Quotas_cnt++ ;
                                    }
/*--------------------------------------------------------------- Запись файла */

           if(read_only)  return(0) ;

   try 
   {
               file_w= new StreamWriter(QuotaPath) ;                          /* Открытие файла */
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - quota file open error for write:\r\n"+exc.Message) ;
                          return(-1) ;
   }

     for(i=0 ; i<Quotas_cnt ; i++)  file_w.WriteLine(Quotas[i].Row) ;

                           file_w.Close() ;                                   /* Закрытие файла */

/*-----------------------------------------------------------------------------*/

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                       Определение групповой квоты                           */

  static  int  GetQuotaGroup(Quota  phone_quota)
{
  int i ;


    for(i=0 ; i<Groups_cnt ; i++)
      if(Groups[i].Comment==phone_quota.Comment)  return(i) ;

  return(-1) ;
}
/*******************************************************************************/
/*                                                                             */
/*                          Создание шаблона файла календаря                   */

  static  int  CalendarTemplate(string path)
{
  string  text ;
     int  pos ;

   try 
   {
               pos=path.LastIndexOf(".") ;

       if(String.Compare(path.Substring(pos), ".CSV", true)==0)
             text="Disable;Day;Start;Stop;Calls per hour;Comment\r\n"+
                  ";*;10:00;13:00;20;Any day\r\n"+
                  ";*;14:00;18:00;20;Any day\r\n"+
                  ";1;10:00;13:00;20;Monday\r\n"+
                  ";7;10:00;13:00;20;Sunday\r\n"+
                  ";12.05.2014;10:00;13:00;20;Specific day\r\n" ;
       else  text="Disable\tDay\tStart\tStop\tCalls per hour\tComment\r\n"+
                  "\t*\t10:00\t13:00\t20\tAny day\r\n"+
                  "\t*\t14:00\t18:00\t20\tAny day\r\n"+
                  "\t1\t10:00\t13:00\t20\tMonday\r\n"+
                  "\t7\t10:00\t13:00\t20\tSunday\r\n"+
                  "\t12.05.2014\t10:00\t13:00\t20\tSpecific day\r\n" ;
         
             File.WriteAllText(path, text) ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - CalendarTemplate: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                          Обмен данными с файлом календаря                   */

  static  int  CalendarFileCheck()
{
  StreamReader  file_r ;
        string  text ;
          char  delimeter ;
      string[]  words ;
      string[]  data ;
           int  pos ;
           int  n  ;
           int  i  ;

/*----------------------------------------------------------- Входной контроль */

    if(               CalendarPath==null            )  return(0) ;
    if(               CalendarPath==""              )  return(0) ;
    if(String.Compare(CalendarPath, "null", true)==0)  return(0) ;

    if(Days==null)  Days=new Calendar[DAYS_MAX] ;

                        data=new string[10] ;

/*--------------------------------------------------- Идентификация типа файла */

               pos=CalendarPath.LastIndexOf(".") ;

       if(String.Compare(CalendarPath.Substring(pos), 
                                      ".CSV", true )==0)  delimeter=';' ;
       else                                               delimeter='\t' ;

/*----------------------------------------------------------- Считывание файла */

   try 
   {
               file_r= new StreamReader(CalendarPath) ;                       /* Открытие файла */
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - calendar file open error for read:\r\n"+exc.Message) ;
                          return(-1) ;
   }

                Days_cnt=0 ;

     do {
             text=file_r.ReadLine() ;
          if(text==null)  break ;

          if(Days[Days_cnt]==null)  Days[Days_cnt]    =new Calendar() ;
                                    Days[Days_cnt].Row= text ;
                                         Days_cnt++ ;
               
        } while(true) ;

                           file_r.Close() ;                                   /* Закрытие файла */

/*-------------------------------------------------------------- Анализ данных */

     for(n=0 ; n<Days_cnt ; n++) {
 
                  text=Days[n].Row+";;;;;;" ;
            words=text.Split(delimeter) ;                                      /* Разбиваем строку на слова */
         if(words==null)  continue ;

       for(i=0 ; i<6 ; i++) {                                                  /* Канонизация набора данных */
              if(i==5)  data[i]= "no remark" ;
              else      data[i]= "" ;    

              if(words.Count()>i && 
                 words[i]!=""      )  data[i]=words[i] ;
                            }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Проверка данных */
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Занесение данных */
                  if(data[0]!="")  Days[n].Disable=true ;
                  else             Days[n].Disable=false ;
          
                  if(Days[n].Disable)  continue ;

                  if(data[1]       =="*")  Days[n].AnyDay=true ;
             else if(data[1].Length== 1 )  Days[n].WeekDay=Convert.ToInt32(data[1]) ;
             else                          Days[n].Date   =data[1] ;

                        Days[n].TimeStart   =                data[2] ;
                        Days[n].TimeStop    =                data[3] ;

              try 
              {
                        Days[n].CallsPerHour=Convert.ToInt32(data[4]) ;
              }
              catch
              {
                        Days[n].CallsPerHour= 0 ;
              }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                 }
/*-----------------------------------------------------------------------------*/

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                      Получение квоты по с файлу календаря                   */

  static  int  CalendarQuota(DateTime now)
{
     int  quota ;
     int  category ;
  string  time ;
     int  week_day ;
  string  date ;
     int  i ;

/*----------------------------------------------------------- Входной контроль */

       if(Days_cnt<=0)  return(-1) ;

/*----------------------------------------------------------------- Подготовка */

                           time=now.Hour.ToString("D2")+":"+now.Minute.ToString("D2") ;

                       week_day=(int)now.DayOfWeek ;
      if(week_day==0)  week_day= 7 ;

                           date=now.Day.ToString("D2")+"."+now.Month.ToString("D2")+"."+now.Year.ToString("D4") ;

/*---------------------------------------------------------- Перебор категорий */

             quota=0 ;

   for(category=0 ; ; category++) {

       if(category>2)  break ;

/*--------------------------------------------------------- Поиск по категории */

    for(i=0 ; i<Days_cnt ; i++) {

       if(Days[i].Disable)  continue ;

       if(category==0) if(Days[i].AnyDay !=true    )  continue ;
       if(category==1) if(Days[i].WeekDay!=week_day)  continue ;
       if(category==2) if(Days[i].Date   !=date    )  continue ;

       if(String.Compare(time, Days[i].TimeStart)< 0)  continue ;
       if(String.Compare(time, Days[i].TimeStop )>=0)  continue ;

                                    quota=Days[i].CallsPerHour ;
                                }
/*---------------------------------------------------------- Перебор категорий */
                                  }        
/*-----------------------------------------------------------------------------*/

   return(quota) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Обмен данными с файлом диапазонов номеров                   */

  static  int  RangesFileCheck()
{
  StreamReader  file_r ;
        string  text ;
          char  delimeter ;
      string[]  words ;
      string[]  data ;
           int  n  ;
           int  i  ;

/*----------------------------------------------------------- Входной контроль */

    if(               RangesPath==null            )  return(0) ;
    if(               RangesPath==""              )  return(0) ;
    if(String.Compare(RangesPath, "null", true)==0)  return(0) ;

    if(Ranges==null)  Ranges=new Range[RANGES_MAX] ;

                        data=new string[10] ;

/*--------------------------------------------------- Идентификация типа файла */

                       delimeter=';' ;

/*----------------------------------------------------------- Считывание файла */

   try 
   {
               file_r= new StreamReader(RangesPath) ;                         /* Открытие файла */
   }
   catch (Exception exc)
   {
          MessageWait("ERROR - ranges file open error for read:\r\n"+exc.Message) ;
                          return(-1) ;
   }

                Ranges_cnt=0 ;

     do {
             text=file_r.ReadLine() ;
          if(text==null)  break ;

          if(Ranges[Ranges_cnt]==null)  Ranges[Ranges_cnt]    =new Range() ;
                                        Ranges[Ranges_cnt].Row= text ;
                                               Ranges_cnt++ ;
               
        } while(true) ;

                           file_r.Close() ;                                   /* Закрытие файла */

/*-------------------------------------------------------------- Анализ данных */

     for(n=0 ; n<Ranges_cnt ; n++) {
 
                  text=Ranges[n].Row+";;;;;;" ;
            words=text.Split(delimeter) ;                                      /* Разбиваем строку на слова */
         if(words==null)  continue ;

       for(i=0 ; i<3 ; i++) {                                                  /* Канонизация набора данных */
              if(i==2)  data[i]= "no remark" ;
              else      data[i]= "" ;    

              if(words.Count()>i && 
                 words[i]!=""      )  data[i]=words[i] ;
                            }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Проверка данных */
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Занесение данных */
                     Ranges[n].PhoneMin=data[0] ;
                     Ranges[n].PhoneMax=data[1] ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                                   }
/*-----------------------------------------------------------------------------*/

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*         Проверка соответсвия номера допустимым диапазонам номеров           */

  static  bool  RangesCheck(string  target)
{
       int  n  ;

/*----------------------------------------------------------- Входной контроль */

    if(Ranges_cnt==0)  return(true) ;

/*---------------------------------------------------------- Анализ диапазонов */

     for(n=0 ; n<Ranges_cnt ; n++) 
       if(String.Compare(target, RangesPrefix+Ranges[n].PhoneMin, true)>=0 &&
          String.Compare(target, RangesPrefix+Ranges[n].PhoneMax, true)<=0   )  return(true) ;

/*-----------------------------------------------------------------------------*/

   return(false) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                   Инициализация диапазона сканирования                      */

  static  void  ResetNextTarget(string group)
{
      int  i ;

/*-------------------------------------- Инициализация счетчиков использования */

  if(TargetsGroups_cnt==0)
   if(ScanType=="Single")  return ;                                           /* Только для ScanType=Heap */

   if(group==null) {

      for(i=0 ; i<Targets_cnt ; i++)  Targets[i].use_cnt=Targets[i].use_init ;  
                   }
   else            {

      for(i=0 ; i<Targets_cnt ; i++)
        if(Targets[i].Group==group)  Targets[i].use_cnt=Targets[i].use_init ;  
                   }
/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                       Сброс диапазона сканирования                          */

  static  void  ClearNextTarget(string group)
{
   int  i ;


   if(ScanType=="Single")  return ;                                           /* Только для ScanType=Heap */

   if(group==null) {

      for(i=0 ; i<Targets_cnt ; i++)  Targets[i].use_cnt=0 ;  
                   }
   else            {

      for(i=0 ; i<Targets_cnt ; i++)
        if(Targets[i].Group==group)  Targets[i].use_cnt=0 ;  
                   }

}
/*******************************************************************************/
/*                                                                             */
/*               Выдача следующего номера из диапазона сканирования            */
/*                                                                             */
/*  Для группового сканирования всегда ScanType=Heap                           */

  static  string  GetNextTarget(string group)
{
          string  source_type ;
             int  index ;
             int  step ;
             int  prefix ;
          string  target ;
          string  call_string ;
          string  dgts ;
  CharEnumerator  digits ;
          char[]  chrs ;
             int  i ;

/*---------------------------------------------------------------- Общая часть */

                              source_type=ScanType ;
     if(TargetsGroups_cnt>0)  source_type="Heap" ;

/*----------------------------------------------------- Одно-диапазонный режим */

   if(source_type=="Single") {

     do {
                              target=ScanNumbers ;
/*- - - - - - - - - - - - - - - - - - - - - - - -  Определение нового значения */
         do {
                  index=RandTarget.Next() ;
              if(index>=ScanIndexMin && index<ScanIndexMax)  break ;

            } while(true) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - Фиксация последнего номера */
         File.WriteAllText(ControlFolder+"\\srand.last", index.ToString()) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - Определение префиксa */
       if(ScanPrefix!=null) {                                                 /* Если префиксы заданы... */ 
                               prefix=index/ScanIndexMin-1 ;
                               target=target.Replace("###", ScanPrefixList[prefix]) ;
                            }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - Замена звездочек */
                             index= index%ScanIndexMin+ScanIndexMin ;
                              dgts= index.ToString() ;
                            digits=  dgts.GetEnumerator() ;
                              chrs=target.ToCharArray() ;

                                           digits.MoveNext() ;                /* Сдвигаем с первой цифры - она фиктивная */

          for(target=null, i=0 ; i<chrs.Count() ; i++)
               if(chrs[i]=='*') {
                                           digits.MoveNext() ;
                                   target+=digits.Current ;
                                }
               else             {
                                   target+=chrs[i] ;
                                }
/*- - - - - - - - - - - - - - - - -  Проверка на разрешенные диапазоны номеров */
             if(RangesCheck(target))  return(target) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
        } while(true) ;
                            }
/*--------------------------------------------------------- Режим большой кучи */

   else                     {
/*- - - - - - - - - - - - - - - - - - - - - - - - Определение опорного индекса */
/*  При работе с группами иходим из предположения, что все номера группы       */
/* расположены единым непрерывным кластером                                    */
     while(true) {
                           index=(int)(RandTarget.NextDouble()*Targets_cnt) ;

         File.WriteAllText(ControlFolder+"\\srand.last", index.ToString()) ;

           if(index>=Targets_cnt)  index=Targets_cnt-1 ;

           if(group==Targets[index].Group)  break ;

                 }
/*- - - - - - - - - - - - - - - - - - - - - - -  Определение следующего номера */
   if(Targets[index].use_cnt==0) {                                             /* Если опорный индекс уже использован -        */
                                                                               /*  - берем ближайшее неиспользованное значение */
         for(step=1 ; step<Targets_cnt ; step++) {

                if(index-step>=   0       ) {
                    if(Targets[index-step].use_cnt>0   &&
                       Targets[index-step].Group ==group ) {  index-=step ;  break ;  }
                                            }
                if(index+step< Targets_cnt) {
                    if(Targets[index+step].use_cnt>0   &&
                       Targets[index+step].Group ==group ) {  index+=step ;  break ;  }
                                            }

                if(index-step<   0         &&
                   index+step>=Targets_cnt   )  break ;
                                             
                                                 }
                                 }

   if(Targets[index].use_cnt==0)  return(null) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - -  Учет отработки номера */
                                 call_string=Targets[index].Phone ;

               Targets[index].use_cnt-- ;
        return(call_string) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                            }
/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                       Запись списка номеров в файл                          */

  static  int  SaveTargetsToFile(string  path)
{
  StreamWriter  file ;
        string  text ;
           int  i ;


   if(ScanType=="Single")  return(0) ;    

   try 
   {
               file= new StreamWriter(path) ;                                 /* Открытие файла */
   }
#pragma warning disable 0168
   catch (Exception exc)
   {
              return(-1) ;
   }
#pragma warning restore 0168

         for(i=0 ; i<Targets_cnt ; i++) {

                              text=Targets[i].Phone+" "+Targets[i].use_cnt+" "+Targets[i].use_init ;
               file.WriteLine(text) ;                                                             
                                        }

                           file.Close() ;                                     /* Закрытие файла */

  return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                       Считывание списка номеров из файла                    */

  static  int  FormTargetsByFile(string  path, bool exist)
{
  StreamReader  file_r ;
        string  text ;
          char  delimeter ;
      string[]  words ;

/*----------------------------------------------------------- Входной контроль */

    if(               path==null            )  return(0) ;
    if(               path==""              )  return(0) ;
    if(String.Compare(path, "null", true)==0)  return(0) ;

/*----------------------------------------------------------------- Подготовка */

                           delimeter=' ' ;

/*----------------------------------------------------------- Считывание файла */

   try 
   {
               file_r= new StreamReader(path) ;                               /* Открытие файла */
   }
   catch (Exception exc)
   {
      if(exist)  MessageWait("ERROR - targets file open error for read:\r\n"+exc.Message) ;
                          return(-1) ;
   }

                Targets_cnt=0 ;

     do {
             text=file_r.ReadLine() ;
          if(text==null)  break ;

             words=text.Split(delimeter) ;                                    /* Разбиваем строку на слова */
          if(words==null)  continue ;

          if(words.Count()<1)  continue ;
 
              Targets[Targets_cnt]         =new Target() ;
              Targets[Targets_cnt].Phone   =words[0] ;

          if(words.Count()>1)
              Targets[Targets_cnt].use_cnt =Convert.ToInt32(words[1]) ;

          if(words.Count()>2)
              Targets[Targets_cnt].use_init=Convert.ToInt32(words[2]) ;

                    Targets_cnt++ ;
               
        } while(true) ;

                           file_r.Close() ;                                   /* Закрытие файла */

/*-----------------------------------------------------------------------------*/

  return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                       Считывание списка номеров из перечня                  */

  static  int  FormTargetsByRanges(string  ranges)
{
  CharEnumerator  chars ;
        string[]  words ;
             int  i ;

/*---------------------------------------------------------------- Общая часть */

                ranges=ranges.Trim() ;
                ranges=ranges.Trim(',') ;

/*----------------------------------------------------- Одно-диапазонный режим */

   if(ScanType=="Single") {
/*- - - - - - - - - - - - - - - - - - - - - - Определение цифровой размерности */
                    chars=ranges.GetEnumerator() ;
             ScanIndexMin= 1 ;

       while(chars.MoveNext())
           if(chars.Current=='*')  ScanIndexMin*=10 ;

//     if(ScanIndexMin<100000) {
       if(ScanIndexMin<100   ) {
              MessageWait("ERROR Configuration - For scan range less then 100000 numbers use ScanType=Heap") ;
                                        return(-1) ;
                               }
/*- - - - - - - - - - - - - - - - - - -  Определение полного диапазона номеров */
      if(ScanPrefix==null) {                                                  /* Если префиксы не заданы... */ 
                              ScanIndexMax=2*ScanIndexMin ;
                           } 
      else                 {
                                     ScanPrefix=ScanPrefix.Trim() ;
                                     ScanPrefix=ScanPrefix.Trim(',') ;

                               ScanPrefixList=ScanPrefix.Split(',') ;         /* Разбиваем строку на слова */
                            if(ScanPrefixList==null)  return(-1) ;

                              ScanIndexMax=(ScanPrefixList.Count()+1)*ScanIndexMin ;
                           } 
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                          }
/*--------------------------------------------------------- Режим большой кучи */

   else                   {
                              if(ranges.Length<4)  return(-1) ;

                                 words=ranges.Split(',') ;                    /* Разбиваем строку на слова */
                              if(words==null)  return(-1) ;

                             for(i=0 ; i<words.Count() ; i++)                 /* Разбираем список диапазонов */
                                          AddToTargets(words[i].Trim()) ;
                          }
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*               Добавление диапазона в список сканирования                    */

  static  int  AddToTargets(string range)
{
  CharEnumerator  chars ;
          char[]  chrs ;
  CharEnumerator  digits ;
             int  max ;
             int  cnt ;
          string  target ;
          string  index ;
             int  n ;
             int  i ;

/*--------------------------------- Определение емкости диапазона сканирования */

            chars=range.GetEnumerator() ;
              max= 1 ;
              cnt= 0 ;

   while(chars.MoveNext())
       if(chars.Current=='*') {  max*=10 ;  cnt++ ;  }

/*---------------------------------------------- Определение следующего номера */

  for(n=0 ; n<max ; n++) {                                                    /* LOOP - Перебор всех номеров диапазона */ 

                target=range ;

                 index=String.Format("{0:D}", n) ;
                 index=index.PadLeft(cnt, '0') ;

                  chrs=target.ToCharArray() ;
                digits= index.GetEnumerator() ;

          for(target=null, i=0 ; i<chrs.Count() ; i++)
               if(chrs[i]=='*') {
                                           digits.MoveNext() ;
                                   target+=digits.Current ;
                                }
               else             {
                                   target+=chrs[i] ;
                                }

      if(Targets[Targets_cnt]==null) Targets[Targets_cnt]         =new Target() ;
                                     Targets[Targets_cnt].Group   =null ;
                                     Targets[Targets_cnt].Phone   =target ;
                                     Targets[Targets_cnt].use_init=  1 ;
                                             Targets_cnt++ ;
                         }                                                    /* END LOOP - Перебор всех номеров диапазона */ 
/*-----------------------------------------------------------------------------*/

   return(0) ;
}
/*******************************************************************************/
/*                                                                             */
/*                            Формирование групп номеров                       */

  static  int  FormTargetsGroups()
{
   string    target ;
   string[]  words ;
   string    station ;
      int    n ;
      int    i ;
      int    j ;

/*-------------------------------------- Формирование набора номеров в группах */

     for(n=1 ; n<=TargetsGroups_cnt ; n++) {                                  /* LOOP - Перебор групп */

                                  ScanType=TargetsGroups[n].ScanType ;
                               Targets_cnt= 0 ;

                       FormTargetsByRanges(TargetsGroups[n].Targets) ;
                           ResetNextTarget(null) ;                            /* Инициализируем диапазон сканирования */

                    TargetsGroups[n].List="" ;

       for(i=0 ; i< TargetsGroups[n].Size ; i++) {                            /* LOOP - Цикл подбора значений */

                     target=GetNextTarget(null) ;                             /* Запрашиваем следующий номер */
                  if(target==null)  break ;                                   /* Если все номера перебраны... */

         if(i>0)  TargetsGroups[n].List+="," ;
                  TargetsGroups[n].List+=target ;
                                                 }                            /* END LOOP - Цикл подбора значений */

                                           }                                  /* END LOOP - Перебор групп */
/*----------------------------------------- Формирование общего списка номеров */

     for(j=1 ; j<=TargetsAllias_cnt ; j++)                                    /* Инициализация счетчиков номеров алиасов */
                    TargetsAllias[j].cnt=TargetsAllias[j].Size ;

                                  ScanType="Heap" ;
                               Targets_cnt=  0 ;

     for(n=1 ; n<=TargetsGroups_cnt ; n++) {                                  /* LOOP - Перебор групп */

          words=TargetsGroups[n].List.Split(',') ;                            /* Разбиваем строку на слова */
       if(words==null) {
                           MessageWait("ERROR - Empty list for Targets Group " + TargetsGroups[n].Station) ;
                             return(-1) ;
                       } 

        for(i=0 ; i<words.Count() ; i++) {

                      station="unknown" ;
                       target= words[i] ;

          if(TargetsGroups[n].Station.Substring(0,1)=="G") 
          {
             for(j=1 ; j<=TargetsAllias_cnt ; j++)
               if(TargetsAllias[j].Group==TargetsGroups[n].Station &&
                  TargetsAllias[j].cnt  >   0                        ) {
                      station=TargetsAllias[j].Station ;
                       target= target.Replace("A", TargetsAllias[j].Prefix) ;
                              TargetsAllias[j].cnt-- ;
                                     break ;
                                                                       }
          }
          else
          {
                      station=TargetsGroups[n].Station ;
          }
                 
          if(Targets[Targets_cnt]==null)  Targets[Targets_cnt]         =new Target() ;
                                          Targets[Targets_cnt].Group   =station ;
                                          Targets[Targets_cnt].Phone   =target ;
                                          Targets[Targets_cnt].use_init=  1 ;
                                                  Targets_cnt++ ;
                                         }

                                           }                                  /* END LOOP - Перебор групп */
/*-----------------------------------------------------------------------------*/

   return(0) ;
}

/*******************************************************************************/
/*                                                                             */
/*                    Инициализация установок управления                       */

  static  void  InitControl()
{
  string[]  files ;
    string  text ;

/*--------------------------------------------------- Очистка папки управления */

  if(ClearFlag==1) {

         files=Directory.GetFiles(ControlFolder, "*.*") ;

    foreach(string file in files)  File.Delete(file) ;

                   }
  else             {

         files=Directory.GetFiles(ControlFolder, "*.signal") ;

    foreach(string file in files)  File.Delete(file) ;

         files=Directory.GetFiles(ControlFolder, "*.rel") ;

    foreach(string file in files)  File.Delete(file) ;

         files=Directory.GetFiles(ControlFolder, "*.err") ;

    foreach(string file in files)  File.Delete(file) ;

                   }
/*-------------------------------- Считывание сохраненного значения генератора */

   try 
   {
            text=File.ReadAllText(ControlFolder+"\\srand.last") ;
          RandTarget=new Random(Convert.ToInt32(text)) ;
   }
   catch
   { 
          RandTarget=new Random() ;
   }
/*----------------------------------- Считывание сохраненного значения очереди */

   try 
   {
            text=File.ReadAllText(ControlFolder+"\\queue.last") ;
          Queue_Seq=Convert.ToInt64(text) ;
   }
   catch
   { 
          RandTarget=new Random() ;
   }
/*-----------------------------------------------------------------------------*/

}
/*******************************************************************************/
/*                                                                             */
/*                  Запись управляющего сигнала в очередь                      */

  static  void  AddControl(string signal_type, string signal)
{
  string  path ;


   if(String.Compare(signal_type, "Queue", true)==0) {

                        Queue_Seq++ ;

            path=ControlFolder+"\\Q"+Queue_Seq.ToString("000000000")+"_"+signal+".call" ;

        File.WriteAllText(path, signal) ;
        File.WriteAllText(ControlFolder+"\\queue.last", Queue_Seq.ToString()) ;
                                                     }

   if(String.Compare(signal_type, "Urgent", true)==0) {

            path=ControlFolder+"\\U"+"_"+signal+".signal" ;

        File.WriteAllText(path, signal) ;
                                                      }
}
/*******************************************************************************/
/*                                                                             */
/*                  Выдача следующей команды управления                        */

public class myReverserClass : IComparer<string>  {
      int IComparer<string>.Compare(string x, string y)  {
          return( String.Compare(y, x, true) );
      }
}

public class myOrdererClass : IComparer<string>  {
      int IComparer<string>.Compare(string x, string y)  {
          return( String.Compare(x, y, true) );
      }
}

  static  string  GetNextControl(bool Queue, string Station_id, ref string QueueId)
{
  IComparer<string>  myComparer ;
           string[]  files ;
             string  phone ;
             string  path ;
                int  index ;
                int  n ;
                int  i ;

/*---------------------------------------------- Проверка внеочередных событий */

                         files=Directory.GetFiles(ControlFolder, "*.signal") ;

           if(files.Count()>0) {
                                  File.Delete(files[0]) ;

                                    return("Stop") ;
                               }

   if(Queue==false)  return(null) ;

/*---------------------------------------------------------- Для групп номеров */

  if(TargetsGroups_cnt>0) {
/*- - - - - - - - - - - - - - - - - - - - - - -  Определение следующего номера */                           
                                 phone=null ;

          for(i=0 ; i<Targets_cnt ; i++)
            if(phone!=null) {
                               Targets[i-1].Group   =Targets[i].Group ;
                               Targets[i-1].Phone   =Targets[i].Phone ;
                               Targets[i-1].QueueId =Targets[i].QueueId ;
                            }
            else
            if(Targets[i].Group==Station_id) { 
                                                 phone=Targets[i].Phone ;
                                               QueueId=Targets[i].QueueId ;
                                             }

            if(phone!=null) {    
                                Targets_cnt-- ;
                               return(phone) ;
                            }
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Опрос очереди */
                         files=Directory.GetFiles(ControlFolder, "*."+Station_id+".call") ;

           if(files.Count()==0)  return(null) ;

                                myComparer=new myOrdererClass() ;
              Array.Sort(files, myComparer) ;

                                  n=Targets_cnt ;

        foreach(string file in files) {

                         path=file.Substring(0, file.IndexOf('.')) ;
                        index=path.LastIndexOf('_') ;
           if(index>=0)  path=path.Substring(index+1) ;

           if(Targets[n]==null)  Targets[n]         =new Target() ;
                                 Targets[n].Group   =Station_id ;
                                 Targets[n].Phone   =path ;
                                 Targets[n].QueueId =file ;
                                         n++ ;
                                      }
           if(n==0)  return(null) ;

                                       Targets_cnt=n-1 ;
                       QueueId=Targets[Targets_cnt].QueueId ;
                        return(Targets[Targets_cnt].Phone) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                          } 
/*------------------------------------------------- Для единого потока номеров */
  else                    {
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -  Опрос очереди */
   if(Targets_cnt==0) {
                         files=Directory.GetFiles(ControlFolder, "*.call") ;

           if(files.Count()==0)  return(null) ;

                                myComparer=new myReverserClass() ;
              Array.Sort(files, myComparer) ;

                                  Targets_cnt=0 ;

        foreach(string file in files) {
                                                                    path=file.Substring(0, file.IndexOf('.')) ;
                                                                   index=path.LastIndexOf('_') ;
                                                      if(index>=0)  path=path.Substring(index+1) ;

           if(Targets[Targets_cnt]==null)  Targets[Targets_cnt]         =new Target() ;
                                           Targets[Targets_cnt].Phone   =path ;
                                           Targets[Targets_cnt].QueueId =file ;
                                           Targets[Targets_cnt].use_cnt =  0 ;
                                           Targets[Targets_cnt].use_init=  1 ;
                                                   Targets_cnt++ ;
                                      }
                      }
/*- - - - - - - - - - - - - - - - - - - - - - -  Определение следующего номера */
    if(Targets_cnt==0)  return(null) ;

                                       Targets_cnt-- ;
                       QueueId=Targets[Targets_cnt].QueueId ;
                        return(Targets[Targets_cnt].Phone) ;
/*- - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - -*/
                          } 
/*-----------------------------------------------------------------------------*/
}
/*******************************************************************************/
/*                                                                             */
/*                  Удаление команды управления из очереди                     */

  static  void  CheckOffControl(string  control_name)
{
/*------------------------------------------------ Удаление управляющего файла */

//                Message(" delete " + control_name) ;

            File.Delete(control_name) ;

/*-----------------------------------------------------------------------------*/
}

/*******************************************************************************/
/*                                                                             */
/*                        Запись файла статистики                              */

  static  int  WriteScanStatistics(Station  phone)
{
  string  text ;
  string  time_prefix ;


    if(String.Compare(StatisticsPath, "null", true)==0)  return(0) ;

            time_prefix=DateTime.Now.ToString()+ "  " ;

   try 
   {
        if(phone       == null    )  text=String.Format("{0}\r\n{1}START version {2} SCAN {3}\r\n", StatisticsHeader, time_prefix, Version, ScanNumbers) ;
        else
        if(phone.Completed>0      )  text=String.Format("{0} COMPLETED: {1}\r\n", phone.Idx, phone.Complete_reason) ;
        else
        if(phone.Status=="Error"  )  text=String.Format("{0} Error {1}\r\n", phone.Target, phone.Error) ;
        else 
        if(phone.Status=="Offline")  text=String.Format("{0} OffLine\r\n", phone.Target) ;
        else 
        if(phone.Status=="Ignored")  text=String.Format("{0} Ignored\r\n", phone.Target) ;
        else                         text=String.Format("{0} Start:{1:D} Stop:{2:D}\r\n", phone.Target, phone.Connect_time, phone.Active_time) ;

                                     text=time_prefix+text ;

             File.AppendAllText(StatisticsPath, text) ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - WriteStatistics: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                        Запись файла кандидатов в "Роботы"                   */

  static  int  WriteScanRobots(Station  phone)
{
  string  text ;


    if(String.Compare(ScanRobotsPath, "null", true)==0)  return(0) ;

   try 
   {
                text=String.Format("{0} Start:{1:D} Stop:{2:D}\r\n", phone.Target, phone.Connect_time, phone.Active_time) ;

             File.AppendAllText(ScanRobotsPath, text) ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - WriteScanRobots: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                        Запись файла результата                              */

  static  int  WriteResults(string text)
{

    if(String.Compare(ResultsPath, "null", true)==0)  return(0) ;

   try 
   {
       if(text==null)  File.WriteAllText (ResultsPath,      "\r\n") ;
       else            File.AppendAllText(ResultsPath, text+"\r\n") ;
   }
   catch (Exception exc)
   {
              MessageWait("ERROR - WriteResults: " + exc.Message) ;
                        return(-1) ;
   }

   return(0) ;    
}
/*******************************************************************************/
/*                                                                             */
/*                     Работа со списком целевых номеров                       */

 static string TargetsControl(string action, string phone)
{
  string  spec ;
     int  pos ;
  double  pause ;
     int  i  ;


  lock("TargetsControl")
  {
/*------------------------------------------------- Добавление номера в список */

   if( String.Compare(action, "Add",  true)==0)
   {
      if(Targets_cnt>=TARGETS_MAX) {
                         MessageWait("ERROR - Targets list overflow") ;
                                          return("Error") ;
                                   }

        for(i=0 ; i<Targets_cnt ; i++)
          if(String.Compare(Targets[i].Phone, phone,  true)==0)  return("Dublicated") ;

          if(Targets[Targets_cnt]==null)  Targets[Targets_cnt]=new Target() ;

             Targets[Targets_cnt].Phone      =phone ;
             Targets[Targets_cnt].ReCallSpec =ReCallSpec ;
             Targets[Targets_cnt].NextAttempt=DateTime.MinValue ;
             Targets[Targets_cnt].use_cnt    =  0  ;
                   Targets_cnt++ ;
                   Targets_new++ ;
   }
/*-------------------------------------- Назначение номера на повторный дозвон */

   if( String.Compare(action, "Recall",  true)==0)
   {
        for(i=0 ; i<Targets_cnt ; i++)
          if(String.Compare(Targets[i].Phone, phone,  true)==0)  break ;

          if(i>=Targets_cnt)  return("No entry") ;

        spec=Targets[i].ReCallSpec ;

      if(spec==null) {
       for(i++ ; i<Targets_cnt ; i++)   Targets[i-1]=Targets[i] ;
                                        
                   Targets_cnt-- ;

                       File.Delete(CallsFolder+"\\"+phone+".call") ;

                             Message(phone + " - Recall attempts is over") ;
                              return("Recall attempts is over") ;
                     }
      else           {

              pos=spec.IndexOf(",") ;
           if(pos>=0) {
                         pause=Convert.ToDouble(spec.Substring(0, pos)) ;
                          spec=spec.Substring(pos+1) ;
                      }
           else       {
                         pause=Convert.ToDouble(spec) ;
                          spec=null ;
                      }

             Targets[i].ReCallSpec =spec ;
             Targets[i].NextAttempt=DateTime.Now.AddMinutes(pause) ;
                     }
   }
/*-------------------------------------------------- Удаление номера из списка */

   if( String.Compare(action, "Delete",  true)==0)
   {
        for(i=0 ; i<Targets_cnt ; i++)
          if(String.Compare(Targets[i].Phone, phone,  true)==0)  break ;

          if(i>=Targets_cnt)  return("No entry") ;

       for(i++ ; i<Targets_cnt ; i++)   Targets[i-1]=Targets[i] ;
                                        
                   Targets_cnt-- ;
   }
/*--------------------------------------------------- Запрос номера из очереди */

   if( String.Compare(action, "Next",  true)==0)
   {
        for(i=0 ; i<Targets_cnt ; i++) {

          if(Targets[i].NextAttempt!=DateTime.MinValue)
            if(Targets[i].NextAttempt<DateTime.Now) {
                                              Targets[i].NextAttempt=DateTime.MinValue ;
                                              Targets[i].use_cnt    = 0 ;
                                                    }

          if(Targets[i].use_cnt==0) {
                                              Targets[i].use_cnt=1 ;
                                       return(Targets[i].Phone) ;
                                    }
                                       }
   }
/*-----------------------------------------------------------------------------*/

  }

   return(null) ;
}
/*******************************************************************************/
/*                                                                             */
/*                 Работа со списком действий по тоновому набору               */

 static TonesAction  TonesControl(string action, string tones)
{
     int  i  ;


/*--------------------------------------------------- Действие по набору цифры */

   if( String.Compare(action, "DIGIT",  true)==0)
   {

        for(i=0 ; i<TonesActions_cnt ; i++)
          if(TonesActions[i].tones.IndexOf(tones)>=0)  return(TonesActions[i]) ;

              return(null) ;
   }
/*-----------------------------------------------------------------------------*/

   return(null) ;
}
/*******************************************************************************/
    }
/*******************************************************************************/
/*******************************************************************************/

    class DMCC_this : DMCC_service
    {

  public override void iLog(string text)
{
   Console.WriteLine(text) ;
       Log.WriteLine(text);
}

  public override void iLogAndWait(string text)
{
   Console.WriteLine(text) ;
       Log.WriteLine(text);
}

  public override void iLogException(string text)
{
// Console.WriteLine(text) ;
       Log.WriteException(text);
}

    }
/*******************************************************************************/
/*******************************************************************************/

    class Log
    {

      public  static string  Path ;              /* Файл технологического лога */
      public  static   long  MaxSize ;           /* Максимальный размер лога */
              static   long  Size ;              /* Текущий размер лога */

  public static  void  WriteLine(string  message)
{
  string  prefix ;
  string  text ;


   if(Path==null)  return ; 

         prefix=DateTime.Now.ToString()+" "+Process.GetCurrentProcess().Id ;
           text=prefix+"  "+message+"\r\n" ;

   while(true)
    try 
    {
           File.AppendAllText(Path, text) ;
                   break ;
    }
#pragma warning disable 0168
    catch(IOException exc)
    {
           continue ;
    }
    catch(Exception exc)
    {
           break ;
    }
#pragma warning restore 0168

   if(MaxSize<=0)  return ;

      Size+=text.Length ;
   if(Size>MaxSize) {
      Size=0 ;

                       File.Delete(Path+"_") ;
                       File.Move  (Path, Path+"_") ;
                    }
}

  public static  void  WriteException(string  message)
{
  string  prefix ;
  string  text ;


   if(Path==null)  return ; 

         prefix=DateTime.Now.ToString()+" "+Process.GetCurrentProcess().Id ;
           text=prefix+"  "+message+"\r\n" ;

   while(true)
    try 
    {
           File.AppendAllText(Path+".exceptions", text) ;
                   break ;
    }
#pragma warning disable 0168
    catch(IOException exc)
    {
           continue ;
    }
    catch(Exception exc)
    {
           break ;
    }
#pragma warning restore 0168
}

  public static  void  Initialize()
{
   FileStream  fs ;

  try {
              fs=new FileStream(Path, FileMode.Append) ;
         Size=fs.Length ;
              fs.Close() ;
      }
  catch{} 
}

    }

/*******************************************************************************/
}
