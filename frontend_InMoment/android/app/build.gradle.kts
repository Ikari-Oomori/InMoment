import java.util.Properties

plugins {
    id("com.android.application")
    id("kotlin-android")
    id("com.google.gms.google-services")
    id("dev.flutter.flutter-gradle-plugin")
}

val keystoreProperties = Properties()
val keystorePropertiesFile = rootProject.file("key.properties")

if (keystorePropertiesFile.exists()) {
    keystorePropertiesFile.inputStream().use { keystoreProperties.load(it) }
}

fun String?.isUsableKeystoreValue(): Boolean {
    if (this == null) return false

    val normalized = trim()

    if (normalized.isBlank()) return false
    if (normalized.equals("CHANGE_ME", ignoreCase = true)) return false
    if (normalized.equals("TODO", ignoreCase = true)) return false
    if (normalized.contains("placeholder", ignoreCase = true)) return false

    return true
}

val releaseStoreFilePath = keystoreProperties.getProperty("storeFile")
val releaseStorePassword = keystoreProperties.getProperty("storePassword")
val releaseKeyAlias = keystoreProperties.getProperty("keyAlias")
val releaseKeyPassword = keystoreProperties.getProperty("keyPassword")

val hasCompleteReleaseKeystoreProperties =
    releaseStoreFilePath.isUsableKeystoreValue() &&
        releaseStorePassword.isUsableKeystoreValue() &&
        releaseKeyAlias.isUsableKeystoreValue() &&
        releaseKeyPassword.isUsableKeystoreValue()

val releaseStoreFile = releaseStoreFilePath
    ?.takeIf { hasCompleteReleaseKeystoreProperties }
    ?.let { rootProject.file(it) }

val hasValidReleaseKeystore =
    hasCompleteReleaseKeystoreProperties &&
        releaseStoreFile != null &&
        releaseStoreFile.exists()

android {
    namespace = "com.inmoment.app"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
        isCoreLibraryDesugaringEnabled = true
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    signingConfigs {
        if (hasValidReleaseKeystore) {
            create("release") {
                storeFile = releaseStoreFile
                storePassword = releaseStorePassword
                keyAlias = releaseKeyAlias
                keyPassword = releaseKeyPassword
            }
        }
    }

    defaultConfig {
        applicationId = "com.inmoment.app"
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName
    }

    buildTypes {
        debug {
            versionNameSuffix = "-dev"
        }

        release {
            isMinifyEnabled = false
            isShrinkResources = false

            signingConfig = if (hasValidReleaseKeystore) {
                signingConfigs.getByName("release")
            } else {
                null
            }
        }
    }
}

dependencies {
    coreLibraryDesugaring("com.android.tools:desugar_jdk_libs:2.1.4")
}

flutter {
    source = "../.."
}