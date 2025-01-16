using System.Collections.Generic;
using Calamari.Kubernetes;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.KubernetesFixtures
{
    [TestFixture]
    public class ApiResourceOutputParserTests
    {
        [Test]
        public void ShouldParseCorrectly()
        {
            List<string> outputLines = new List<string>
            {
"NAME                                SHORTNAMES   APIVERSION                        NAMESPACED   KIND                               VERBS                                                        CATEGORIES",
"bindings                                         v1                                true         Binding                            create                                                       ",
"componentstatuses                   cs           v1                                false        ComponentStatus                    get,list                                                     ",
"configmaps                          cm           v1                                true         ConfigMap                          create,delete,deletecollection,get,list,patch,update,watch   ",
"endpoints                           ep           v1                                true         Endpoints                          create,delete,deletecollection,get,list,patch,update,watch   ",
"events                              ev           v1                                true         Event                              create,delete,deletecollection,get,list,patch,update,watch   ",
"limitranges                         limits       v1                                true         LimitRange                         create,delete,deletecollection,get,list,patch,update,watch   ",
"namespaces                          ns           v1                                false        Namespace                          create,delete,get,list,patch,update,watch                    ",
"nodes                               no           v1                                false        Node                               create,delete,deletecollection,get,list,patch,update,watch   ",
"persistentvolumeclaims              pvc          v1                                true         PersistentVolumeClaim              create,delete,deletecollection,get,list,patch,update,watch   ",
"persistentvolumes                   pv           v1                                false        PersistentVolume                   create,delete,deletecollection,get,list,patch,update,watch   ",
"pods                                po           v1                                true         Pod                                create,delete,deletecollection,get,list,patch,update,watch   all",
"podtemplates                                     v1                                true         PodTemplate                        create,delete,deletecollection,get,list,patch,update,watch   ",
"replicationcontrollers              rc           v1                                true         ReplicationController              create,delete,deletecollection,get,list,patch,update,watch   all",
"resourcequotas                      quota        v1                                true         ResourceQuota                      create,delete,deletecollection,get,list,patch,update,watch   ",
"secrets                                          v1                                true         Secret                             create,delete,deletecollection,get,list,patch,update,watch   ",
"serviceaccounts                     sa           v1                                true         ServiceAccount                     create,delete,deletecollection,get,list,patch,update,watch   ",
"services                            svc          v1                                true         Service                            create,delete,deletecollection,get,list,patch,update,watch   all",
"mutatingwebhookconfigurations                    admissionregistration.k8s.io/v1   false        MutatingWebhookConfiguration       create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"validatingadmissionpolicies                      admissionregistration.k8s.io/v1   false        ValidatingAdmissionPolicy          create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"validatingadmissionpolicybindings                admissionregistration.k8s.io/v1   false        ValidatingAdmissionPolicyBinding   create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"validatingwebhookconfigurations                  admissionregistration.k8s.io/v1   false        ValidatingWebhookConfiguration     create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"customresourcedefinitions           crd,crds     apiextensions.k8s.io/v1           false        CustomResourceDefinition           create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"apiservices                                      apiregistration.k8s.io/v1         false        APIService                         create,delete,deletecollection,get,list,patch,update,watch   api-extensions",
"controllerrevisions                              apps/v1                           true         ControllerRevision                 create,delete,deletecollection,get,list,patch,update,watch   "
            };

            var parsedResult = ApiResourceOutputParser.ParseKubectlApiResourceOutput(outputLines);

            parsedResult.Should()
                        .NotBeEmpty();
        }
    }
}