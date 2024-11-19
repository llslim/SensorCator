import com.android.build.api.dsl.Packaging

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.tanujn45.a11y"
    compileSdk = 35

    defaultConfig {
        applicationId = "com.tanujn45.a11y"
        minSdk = 31
        targetSdk = 33
        versionCode = 1
        versionName = "1.0"

        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_1_8
        targetCompatibility = JavaVersion.VERSION_1_8
    }
    buildFeatures {
        viewBinding = true
    }

    packaging {
        resources.excludes.add("META-INF/native-image/**")
//        resources.excludes.add("/META-INF/LICENSE.md")
    }
}

configurations {
    all {
        resolutionStrategy {
            force("androidx.appcompat:appcompat:1.6.1")
            force("androidx.core:core:1.9.0")
            force("androidx.recyclerview:recyclerview:1.2.1")
            force("androidx.annotation:annotation:1.2.0")
        }
    }
}

dependencies {
    implementation("androidx.appcompat:appcompat:1.6.1")
    implementation("androidx.constraintlayout:constraintlayout:2.1.4")
    implementation("com.google.android.material:material:1.10.0")
    implementation("com.polidea.rxandroidble2:rxandroidble:1.17.2")
    implementation("com.github.skydoves:powerspinner:1.2.7")
    implementation(files("./libs/isoparser-1.9.56.jar"))
    implementation(files("./libs/muxer-1.9.56.jar"))
    implementation(files("./libs/slf4j-simple-2.0.9.jar"))
    implementation(files("./libs/slf4j-api-2.0.9.jar"))
    implementation("com.android.support:support-annotations:28.0.0")
    implementation(files("libs/mdslib-3.15.0(1)-release.aar"))
    runtimeOnly("org.aspectj:aspectjrt:1.9.22.1")
    implementation("com.google.guava:guava:31.1-android")
    implementation("com.google.code.gson:gson:2.8.9")
    implementation("androidx.core:core-ktx:+")
    implementation("androidx.navigation:navigation-fragment:2.5.3")
    implementation("androidx.navigation:navigation-ui:2.5.3")
    testImplementation("junit:junit:4.13.2")
    androidTestImplementation("androidx.test.ext:junit:1.1.5")
    androidTestImplementation("androidx.test.espresso:espresso-core:3.5.1")
    implementation("com.github.PhilJay:MPAndroidChart:v3.1.0")
    implementation(files("./libs/wekaSTRIPPED.jar"))
    implementation("com.squareup.okhttp3:okhttp:4.10.0")
//    implementation("nz.ac.waikato.cms.weka:weka-stable:3.8.0") {
//        exclude(group = "nz.ac.waikato.cms.weka.thirdparty", module = "java-cup-11b-runtime")
//    }
    val camerax_version = "1.4.0-alpha04"
    implementation("androidx.camera:camera-core:${camerax_version}")
    implementation("androidx.camera:camera-camera2:${camerax_version}")
    implementation("androidx.camera:camera-lifecycle:${camerax_version}")
    implementation("androidx.camera:camera-video:${camerax_version}")
    implementation("androidx.camera:camera-view:${camerax_version}")
    implementation("androidx.camera:camera-mlkit-vision:${camerax_version}")
    implementation("androidx.camera:camera-extensions:${camerax_version}")
}