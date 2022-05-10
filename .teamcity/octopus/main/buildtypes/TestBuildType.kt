package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.AbsoluteId
import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.CheckoutMode
import jetbrains.buildServer.configs.kotlin.v2019_2.failureConditions.BuildFailureOnMetric
import jetbrains.buildServer.configs.kotlin.v2019_2.failureConditions.failOnMetricChange

abstract class TestBuildType(block: BuildType.() -> Unit) : BuildType({
    params {
        param("system.OctopusPackageVersion", "%build.number%")
        param("ExtraPackageSources", "")
    }

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"
    
    vcs {
        AbsoluteId("OctopusDeploy_LIbraries_Sashimi_SharedGitHubVcsRoot")

        checkoutMode = CheckoutMode.MANUAL
        cleanCheckout = true
    }

    features {
        feature {
            type = "xml-report-plugin"
            param("xmlReportParsing.reportType", "trx")
            param("xmlReportParsing.reportDirs", "**/*.trx")
        }
    }

    failureConditions {
        executionTimeoutMin = 60
        failOnMetricChange {
            metric = BuildFailureOnMetric.MetricType.TEST_COUNT
            units = BuildFailureOnMetric.MetricUnit.DEFAULT_UNIT
            comparison = BuildFailureOnMetric.MetricComparison.LESS
            compareTo = value()
        }
        failOnMetricChange {
            metric = BuildFailureOnMetric.MetricType.TEST_COUNT
            threshold = 20
            units = BuildFailureOnMetric.MetricUnit.PERCENTS
            comparison = BuildFailureOnMetric.MetricComparison.LESS
            compareTo = build {
                buildRule = lastSuccessful()
            }
        }
    }
}) {
    init {
        this.apply(block)
    }
}