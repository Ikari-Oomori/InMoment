package com.inmoment.app

import android.content.ComponentName
import android.content.ContentValues
import android.content.Intent
import android.net.Uri
import android.os.Bundle
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    private val channelName = "inmoment/widget"
    private val badgeChannelName = "inmoment/badge"
    private var widgetChannel: MethodChannel? = null
    private var initialWidgetPayloadConsumed = false

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        widgetChannel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, channelName)
        widgetChannel?.setMethodCallHandler { call, result ->
            when (call.method) {
                "setWidgetData" -> {
                    val activeGroupId = call.argument<String>("activeGroupId")
                    val activeGroupName = call.argument<String>("activeGroupName")
                    val activeGroupAvatarUrl = call.argument<String>("activeGroupAvatarUrl")
                    val latestPhotoId = call.argument<String>("latestPhotoId")
                    val latestPhotoUrl = call.argument<String>("latestPhotoUrl")
                    val latestPhotoCreatedAtIso =
                        call.argument<String>("latestPhotoCreatedAtIso")
                    val newReactionsCount =
                        call.argument<Int>("newReactionsCount") ?: 0
                    val cachedPhotoUrl = call.argument<String>("cachedPhotoUrl")
                    val cachedPhotoPath = call.argument<String>("cachedPhotoPath")
                    val latestContentKind = call.argument<String>("latestContentKind")

                    WidgetStorage.save(
                        latestContentKind = latestContentKind,
                        context = applicationContext,
                        activeGroupId = activeGroupId,
                        activeGroupName = activeGroupName,
                        activeGroupAvatarUrl = activeGroupAvatarUrl,
                        latestPhotoId = latestPhotoId,
                        latestPhotoUrl = latestPhotoUrl,
                        latestPhotoCreatedAtIso = latestPhotoCreatedAtIso,
                        newReactionsCount = newReactionsCount,
                        cachedPhotoUrl = cachedPhotoUrl,
                        cachedPhotoPath = cachedPhotoPath
                    )

                    InMomentWidgetProvider.refreshAll(applicationContext)
                    result.success(null)
                }

                "getInitialWidgetPayload" -> {
                    if (initialWidgetPayloadConsumed) {
                        result.success(null)
                    } else {
                        initialWidgetPayloadConsumed = true
                        result.success(readWidgetPayload(intent))
                    }
                }

                "clearWidgetData" -> {
                    WidgetStorage.clear(applicationContext)
                    InMomentWidgetProvider.refreshAll(applicationContext)
                    result.success(null)
                }

                else -> result.notImplemented()
            }
        }

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, badgeChannelName)
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "setBadge" -> {
                        val count = (call.argument<Int>("count") ?: 0).coerceAtLeast(0)
                        updateLauncherBadge(count)
                        result.success(null)
                    }

                    else -> result.notImplemented()
                }
            }
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)

        val payload = readWidgetPayload(intent) ?: return
        widgetChannel?.invokeMethod("openWidgetPhoto", payload)
    }

    private fun readWidgetPayload(intent: Intent?): Map<String, String?>? {
        if (intent?.getStringExtra("source") != "android_widget") return null

        val photoId = intent.getStringExtra("photoId")?.trim()
        if (photoId.isNullOrEmpty()) return null

        return mapOf(
            "source" to "android_widget",
            "targetType" to "photo",
            "photoId" to photoId,
            "groupId" to intent.getStringExtra("groupId")
        )
    }

    private fun updateLauncherBadge(count: Int) {
        updateHuaweiBadge(count)
        updateSamsungBadge(count)
        updateSonyBadge(count)
        updateXiaomiBadge(count)
    }

    private fun launcherClassName(): String = MainActivity::class.java.name

    private fun updateHuaweiBadge(count: Int) {
        try {
            val bundle = Bundle().apply {
                putString("package", packageName)
                putString("class", launcherClassName())
                putInt("badgenumber", count)
            }
            contentResolver.call(
                Uri.parse("content://com.huawei.android.launcher.settings/badge/"),
                "change_badge",
                null,
                bundle
            )
        } catch (_: Throwable) {
        }
    }

    private fun updateSamsungBadge(count: Int) {
        try {
            val component = ComponentName(this, MainActivity::class.java)
            val values = ContentValues().apply {
                put("package", packageName)
                put("class", component.className)
                put("badgecount", count)
            }
            contentResolver.insert(
                Uri.parse("content://com.sec.badge/apps"),
                values
            )
        } catch (_: Throwable) {
        }
    }

    private fun updateSonyBadge(count: Int) {
        try {
            val intent = Intent("com.sonyericsson.home.action.UPDATE_BADGE").apply {
                putExtra("com.sonyericsson.home.intent.extra.badge.SHOW_MESSAGE", count > 0)
                putExtra("com.sonyericsson.home.intent.extra.badge.ACTIVITY_NAME", launcherClassName())
                putExtra("com.sonyericsson.home.intent.extra.badge.MESSAGE", count.toString())
                putExtra("com.sonyericsson.home.intent.extra.badge.PACKAGE_NAME", packageName)
            }
            sendBroadcast(intent)
        } catch (_: Throwable) {
        }
    }

    private fun updateXiaomiBadge(count: Int) {
        try {
            val intent = Intent("android.intent.action.APPLICATION_MESSAGE_UPDATE").apply {
                putExtra("android.intent.extra.update_application_component_name", "$packageName/${launcherClassName()}")
                putExtra("android.intent.extra.update_application_message_text", if (count == 0) "" else count.toString())
            }
            sendBroadcast(intent)
        } catch (_: Throwable) {
        }
    }
}
