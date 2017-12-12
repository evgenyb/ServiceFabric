using System;
using System.Collections.Generic;
using System.Fabric;
using System.Globalization;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Wcf.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using ServiceA.Contracts;
using ServiceA.Contracts.ServiceDependencies.ServiceB;

namespace ServiceA
{
    /// <summary>
    /// El runtime de Service Fabric crea una instancia de esta clase para cada instancia del servicio.
    /// </summary>
    internal sealed class ServiceA : StatelessService
    {
        public ServiceA(StatelessServiceContext context)
            : base(context)
        { }

        /// <summary>
        /// Reemplazo opcional para crear agentes de escucha (por ejemplo, TCP, HTTP) para que esta réplica de servicio controle las solicitudes de cliente o usuario.
        /// </summary>
        /// <returns>Una colección de agentes de escucha.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            yield return new ServiceInstanceListener(context =>
            {
                return CreateSoapListener(context);
            });
        }

        /// <summary>
        /// Este es el punto de entrada principal para la instancia del servicio.
        /// </summary>
        /// <param name="cancellationToken">Se cancela cuando Service Fabric tiene que cerrar esta instancia del servicio.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // TODO: Reemplace el siguiente código de ejemplo por su propia lógica 
            //       o quite este reemplazo de RunAsync si no es necesario en su servicio.

            long iterations = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ServiceEventSource.Current.ServiceMessage(this.Context, "Working-{0}", ++iterations);

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        private static ICommunicationListener CreateSoapListener(StatelessServiceContext context)
        {
            var host = context.NodeContext.IPAddressOrFQDN;
            var endpointConfig = context.CodePackageActivationContext.GetEndpoint("ServiceAEndpoint");
            var port = endpointConfig.Port;
            var scheme = endpointConfig.Protocol.ToString();

            var uri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/", scheme, host, port);
            var listener = new WcfCommunicationListener<IServiceA>(
                serviceContext: context,
                wcfServiceObject: new ServiceAWcf(),
                listenerBinding: new BasicHttpBinding(BasicHttpSecurityMode.None),
                address: new EndpointAddress(uri)
            );

            // Check to see if the service host already has a ServiceMetadataBehavior
            var smb = listener.ServiceHost.Description.Behaviors.Find<ServiceMetadataBehavior>();
            // If not, add one
            if (smb == null)
            {
                smb = new ServiceMetadataBehavior
                {
                    MetadataExporter = {PolicyVersion = PolicyVersion.Policy15},
                    HttpGetEnabled = true,
                    HttpGetUrl = new Uri(uri)
                };

                listener.ServiceHost.Description.Behaviors.Add(smb);
            }
            return listener;
        }
    }

    public class ServiceAWcf : IServiceA , IServiceAForServiceB
    {
        public Foo Foo()
        {
            return new Foo();
        }

        public Bar Bar()
        {
            return new Bar();
        }
    }
}
