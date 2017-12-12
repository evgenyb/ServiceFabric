using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;

namespace ServiceA
{
    [EventSource(Name = "MyCompany-ServiceFabric.ServiceA-ServiceA")]
    internal sealed class ServiceEventSource : EventSource
    {
        public static readonly ServiceEventSource Current = new ServiceEventSource();

        static ServiceEventSource()
        {
            // Esta es una solución para el problema por el que no se hace el seguimiento de la actividad de ETW hasta que se inicializa la infraestructura Tasks.
            // Este problema se corregirá en .NET Framework 4.6.2.
            Task.Run(() => { });
        }

        // El constructor de instancia es privado para exigir la semántica singleton
        private ServiceEventSource() : base() { }

        #region Palabras clave
        // Las palabras clave de eventos se pueden usar para categorizar eventos. 
        // Cada palabra clave es una marca de bits. Un solo evento se puede asociar a varias palabras clave (con la propiedad EventAttribute.Keywords).
        // Las palabras clave deben definirse como una clase pública denominada 'Keywords' dentro del elemento EventSource que las usa.
        public static class Keywords
        {
            public const EventKeywords Requests = (EventKeywords)0x1L;
            public const EventKeywords ServiceInitialization = (EventKeywords)0x2L;
        }
        #endregion

        #region Eventos
        // Defina un método de instancia para cada evento que desee registrar y aplíquele un atributo [Event].
        // El nombre del método es el nombre del evento.
        // Pase los parámetros que desee registrar con el evento (solo se permiten tipos enteros primitivos, DateTime, GUID y string).
        // La implementación de cada método de evento debe comprobar si el origen del evento está habilitado y, si lo está, llamar al método WriteEvent() para generar el evento.
        // El número y el tipo de los argumentos pasados a cada método de evento deben coincidir exactamente con los que se pasen a WriteEvent().
        // Ponga el atributo [NonEvent] en todos los métodos que no definen un evento.
        // Para obtener más información, vea https://msdn.microsoft.com/es-es/library/system.diagnostics.tracing.eventsource.aspx.

        [NonEvent]
        public void Message(string message, params object[] args)
        {
            if (this.IsEnabled())
            {
                string finalMessage = string.Format(message, args);
                Message(finalMessage);
            }
        }

        private const int MessageEventId = 1;
        [Event(MessageEventId, Level = EventLevel.Informational, Message = "{0}")]
        public void Message(string message)
        {
            if (this.IsEnabled())
            {
                WriteEvent(MessageEventId, message);
            }
        }

        [NonEvent]
        public void ServiceMessage(StatelessServiceContext serviceContext, string message, params object[] args)
        {
            if (this.IsEnabled())
            {
                string finalMessage = string.Format(message, args);
                ServiceMessage(
                    serviceContext.ServiceName.ToString(),
                    serviceContext.ServiceTypeName,
                    serviceContext.InstanceId,
                    serviceContext.PartitionId,
                    serviceContext.CodePackageActivationContext.ApplicationName,
                    serviceContext.CodePackageActivationContext.ApplicationTypeName,
                    serviceContext.NodeContext.NodeName,
                    finalMessage);
            }
        }

        // En el caso de eventos muy frecuentes, puede ser más útil generarlos con la API WriteEventCore.
        // Esto permite un control más eficaz de los parámetros, pero requiere la asignación explícita de una estructura EventData y código inseguro.
        // Para habilitar esta ruta de código, defina el símbolo de compilación condicional UNSAFE y active la admisión de código inseguro en las propiedades del proyecto.
        private const int ServiceMessageEventId = 2;
        [Event(ServiceMessageEventId, Level = EventLevel.Informational, Message = "{7}")]
        private
#if UNSAFE
        unsafe
#endif
        void ServiceMessage(
            string serviceName,
            string serviceTypeName,
            long replicaOrInstanceId,
            Guid partitionId,
            string applicationName,
            string applicationTypeName,
            string nodeName,
            string message)
        {
#if !UNSAFE
            WriteEvent(ServiceMessageEventId, serviceName, serviceTypeName, replicaOrInstanceId, partitionId, applicationName, applicationTypeName, nodeName, message);
#else
            const int numArgs = 8;
            fixed (char* pServiceName = serviceName, pServiceTypeName = serviceTypeName, pApplicationName = applicationName, pApplicationTypeName = applicationTypeName, pNodeName = nodeName, pMessage = message)
            {
                EventData* eventData = stackalloc EventData[numArgs];
                eventData[0] = new EventData { DataPointer = (IntPtr) pServiceName, Size = SizeInBytes(serviceName) };
                eventData[1] = new EventData { DataPointer = (IntPtr) pServiceTypeName, Size = SizeInBytes(serviceTypeName) };
                eventData[2] = new EventData { DataPointer = (IntPtr) (&replicaOrInstanceId), Size = sizeof(long) };
                eventData[3] = new EventData { DataPointer = (IntPtr) (&partitionId), Size = sizeof(Guid) };
                eventData[4] = new EventData { DataPointer = (IntPtr) pApplicationName, Size = SizeInBytes(applicationName) };
                eventData[5] = new EventData { DataPointer = (IntPtr) pApplicationTypeName, Size = SizeInBytes(applicationTypeName) };
                eventData[6] = new EventData { DataPointer = (IntPtr) pNodeName, Size = SizeInBytes(nodeName) };
                eventData[7] = new EventData { DataPointer = (IntPtr) pMessage, Size = SizeInBytes(message) };

                WriteEventCore(ServiceMessageEventId, numArgs, eventData);
            }
#endif
        }

        private const int ServiceTypeRegisteredEventId = 3;
        [Event(ServiceTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered service type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ServiceTypeRegistered(int hostProcessId, string serviceType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, serviceType);
        }

        private const int ServiceHostInitializationFailedEventId = 4;
        [Event(ServiceHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Service host initialization failed", Keywords = Keywords.ServiceInitialization)]
        public void ServiceHostInitializationFailed(string exception)
        {
            WriteEvent(ServiceHostInitializationFailedEventId, exception);
        }

        // Un par de eventos que comparten el mismo prefijo de nombre con un sufijo "Start"/"Stop" marca implícitamente los límites de una actividad de seguimiento de eventos.
        // Estas actividades se pueden recopilar automáticamente con herramientas de depuración y generación de perfiles, que pueden calcular el tiempo de ejecución, las actividades secundarias
        // y otras estadísticas.
        private const int ServiceRequestStartEventId = 5;
        [Event(ServiceRequestStartEventId, Level = EventLevel.Informational, Message = "Service request '{0}' started", Keywords = Keywords.Requests)]
        public void ServiceRequestStart(string requestTypeName)
        {
            WriteEvent(ServiceRequestStartEventId, requestTypeName);
        }

        private const int ServiceRequestStopEventId = 6;
        [Event(ServiceRequestStopEventId, Level = EventLevel.Informational, Message = "Service request '{0}' finished", Keywords = Keywords.Requests)]
        public void ServiceRequestStop(string requestTypeName, string exception = "")
        {
            WriteEvent(ServiceRequestStopEventId, requestTypeName, exception);
        }
        #endregion

        #region Métodos privados
#if UNSAFE
        private int SizeInBytes(string s)
        {
            if (s == null)
            {
                return 0;
            }
            else
            {
                return (s.Length + 1) * sizeof(char);
            }
        }
#endif
        #endregion
    }
}
