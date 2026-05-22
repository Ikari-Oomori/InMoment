package com.inmoment.app

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.view.View
import android.widget.RemoteViews
import java.io.File
import java.text.SimpleDateFormat
import java.time.Instant
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Date
import java.util.Locale

class InMomentWidgetProvider : AppWidgetProvider() {

    override fun onUpdate(
        context: Context,
        appWidgetManager: AppWidgetManager,
        appWidgetIds: IntArray
    ) {
        updateAll(context, appWidgetManager, appWidgetIds)
    }

    companion object {
        fun refreshAll(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val component = ComponentName(context, InMomentWidgetProvider::class.java)
            val ids = manager.getAppWidgetIds(component)
            updateAll(context, manager, ids)
        }

        private fun updateAll(
            context: Context,
            appWidgetManager: AppWidgetManager,
            appWidgetIds: IntArray
        ) {
            val data = WidgetStorage.read(context)

            for (widgetId in appWidgetIds) {
                val views = RemoteViews(context.packageName, R.layout.inmoment_widget)

                bindBaseState(context, views, data)

                val bitmap = loadCachedBitmap(data.cachedPhotoPath)
                if (bitmap != null && data.hasPhoto) {
                    views.setImageViewBitmap(R.id.widget_photo, bitmap)
                    views.setViewVisibility(R.id.widget_placeholder, View.GONE)
                    views.setViewVisibility(R.id.widget_photo, View.VISIBLE)
                    views.setViewVisibility(
                        R.id.widget_video_play_badge,
                        if (data.isLatestVideo) View.VISIBLE else View.GONE
                    )
                }

                appWidgetManager.updateAppWidget(widgetId, views)
            }
        }

        private fun bindBaseState(
            context: Context,
            views: RemoteViews,
            data: WidgetData
        ) {
            val openIntent = Intent(context, MainActivity::class.java).apply {
                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP
                putExtra("source", "android_widget")
                putExtra("photoId", data.latestPhotoId)
                putExtra("groupId", data.activeGroupId)
            }

            val requestCode = 101 + (data.latestPhotoId?.hashCode() ?: 0)

            val pendingIntent = PendingIntent.getActivity(
                context,
                requestCode,
                openIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )

            views.setOnClickPendingIntent(R.id.widget_root, pendingIntent)

            views.setTextViewText(R.id.widget_placeholder_icon, "IM")
            views.setTextViewText(R.id.widget_placeholder_title, "InMoment")

            if (!data.hasActiveGroup) {
                views.setTextViewText(R.id.widget_group_name, "Выберите активную группу")
                views.setTextViewText(
                    R.id.widget_meta,
                    "Откройте InMoment и выберите группу для виджета"
                )
                views.setTextViewText(R.id.widget_badge, "")
                views.setViewVisibility(R.id.widget_badge, View.GONE)
                views.setViewVisibility(R.id.widget_photo, View.GONE)
                views.setViewVisibility(R.id.widget_video_play_badge, View.GONE)
                views.setViewVisibility(R.id.widget_placeholder, View.VISIBLE)
                views.setTextViewText(R.id.widget_placeholder_icon, "IM")
                views.setTextViewText(R.id.widget_placeholder_title, "InMoment")
                return
            }

            views.setTextViewText(
                R.id.widget_group_name,
                data.activeGroupName?.takeIf { it.isNotBlank() } ?: "Активная группа"
            )

            if (!data.hasPhoto) {
                views.setTextViewText(R.id.widget_meta, "Пока нет опубликованных моментов")
                views.setTextViewText(R.id.widget_badge, "")
                views.setViewVisibility(R.id.widget_badge, View.GONE)
                views.setViewVisibility(R.id.widget_photo, View.GONE)
                views.setViewVisibility(R.id.widget_video_play_badge, View.GONE)
                views.setViewVisibility(R.id.widget_placeholder, View.VISIBLE)
                views.setTextViewText(R.id.widget_placeholder_icon, "IM")
                views.setTextViewText(R.id.widget_placeholder_title, "Ждём первый момент")
                return
            }

            val formattedDate = formatDate(data.latestPhotoCreatedAtIso)
            views.setTextViewText(
                R.id.widget_meta,
                if (formattedDate == null) {
                    "Последний момент"
                } else {
                    "Последний момент · $formattedDate"
                }
            )

            if (data.newReactionsCount > 0) {
                val badgeText = if (data.newReactionsCount == 1) {
                    "1 новая реакция"
                } else {
                    "${data.newReactionsCount} новых реакций"
                }

                views.setTextViewText(R.id.widget_badge, badgeText)
                views.setViewVisibility(R.id.widget_badge, View.VISIBLE)
            } else {
                views.setTextViewText(R.id.widget_badge, "")
                views.setViewVisibility(R.id.widget_badge, View.GONE)
            }

            if (data.hasCachedPhoto) {
                views.setViewVisibility(R.id.widget_placeholder, View.GONE)
                views.setViewVisibility(R.id.widget_photo, View.VISIBLE)
                views.setViewVisibility(
                    R.id.widget_video_play_badge,
                    if (data.isLatestVideo) View.VISIBLE else View.GONE
                )
            } else {
                views.setViewVisibility(R.id.widget_placeholder, View.VISIBLE)
                views.setViewVisibility(R.id.widget_photo, View.GONE)
                views.setViewVisibility(R.id.widget_video_play_badge, View.GONE)

                if (data.isLatestVideo) {
                    views.setTextViewText(R.id.widget_placeholder_icon, "▶")
                    views.setTextViewText(R.id.widget_placeholder_title, "Последнее — видео")
                } else {
                    views.setTextViewText(R.id.widget_placeholder_icon, "IM")
                    views.setTextViewText(R.id.widget_placeholder_title, "Момент недоступен")
                }
            }
        }

        private fun loadCachedBitmap(path: String?): Bitmap? {
            if (path.isNullOrBlank()) return null

            return try {
                val file = File(path)
                if (!file.exists()) return null
                BitmapFactory.decodeFile(file.absolutePath)
            } catch (_: Exception) {
                null
            }
        }

        private fun formatDate(raw: String?): String? {
            val value = raw?.trim()?.takeIf { it.isNotEmpty() } ?: return null
            val formatter = SimpleDateFormat("dd.MM.yyyy", Locale.getDefault())

            return try {
                val instant = Instant.parse(value)
                formatter.format(Date.from(instant))
            } catch (_: Exception) {
                try {
                    val instant = OffsetDateTime.parse(value).toInstant()
                    formatter.format(Date.from(instant))
                } catch (_: Exception) {
                    try {
                        val localDateTime = LocalDateTime.parse(
                            value.substringBefore("."),
                            DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss")
                        )

                        val instant = localDateTime
                            .atZone(ZoneId.systemDefault())
                            .toInstant()

                        formatter.format(Date.from(instant))
                    } catch (_: Exception) {
                        null
                    }
                }
            }
        }
    }
}