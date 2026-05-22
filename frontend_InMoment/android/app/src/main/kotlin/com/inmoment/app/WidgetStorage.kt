package com.inmoment.app

import android.content.Context

object WidgetStorage {
    private const val PREFS_NAME = "inmoment_widget"

    private const val KEY_ACTIVE_GROUP_ID = "activeGroupId"
    private const val KEY_ACTIVE_GROUP_NAME = "activeGroupName"
    private const val KEY_ACTIVE_GROUP_AVATAR_URL = "activeGroupAvatarUrl"
    private const val KEY_LATEST_PHOTO_ID = "latestPhotoId"
    private const val KEY_LATEST_PHOTO_URL = "latestPhotoUrl"
    private const val KEY_LATEST_PHOTO_CREATED_AT_ISO = "latestPhotoCreatedAtIso"
    private const val KEY_NEW_REACTIONS_COUNT = "newReactionsCount"

    private const val KEY_CACHED_PHOTO_URL = "cachedPhotoUrl"
    private const val KEY_CACHED_PHOTO_PATH = "cachedPhotoPath"
    private const val KEY_LATEST_CONTENT_KIND = "latestContentKind"

    fun save(
        context: Context,
        activeGroupId: String?,
        activeGroupName: String?,
        activeGroupAvatarUrl: String?,
        latestPhotoId: String?,
        latestPhotoUrl: String?,
        latestPhotoCreatedAtIso: String?,
        newReactionsCount: Int,
        cachedPhotoUrl: String?,
        latestContentKind: String?,
        cachedPhotoPath: String?
    ) {
        prefs(context).edit()
            .putString(KEY_ACTIVE_GROUP_ID, activeGroupId)
            .putString(KEY_ACTIVE_GROUP_NAME, activeGroupName)
            .putString(KEY_ACTIVE_GROUP_AVATAR_URL, activeGroupAvatarUrl)
            .putString(KEY_LATEST_PHOTO_ID, latestPhotoId)
            .putString(KEY_LATEST_PHOTO_URL, latestPhotoUrl)
            .putString(KEY_LATEST_PHOTO_CREATED_AT_ISO, latestPhotoCreatedAtIso)
            .putInt(KEY_NEW_REACTIONS_COUNT, newReactionsCount)
            .putString(KEY_CACHED_PHOTO_URL, cachedPhotoUrl)
            .putString(KEY_LATEST_CONTENT_KIND, latestContentKind)
            .putString(KEY_CACHED_PHOTO_PATH, cachedPhotoPath)
            .apply()
    }

    fun clear(context: Context) {
        prefs(context).edit().clear().apply()
    }

    fun read(context: Context): WidgetData {
        val prefs = prefs(context)

        return WidgetData(
            activeGroupId = prefs.getString(KEY_ACTIVE_GROUP_ID, null),
            activeGroupName = prefs.getString(KEY_ACTIVE_GROUP_NAME, null),
            activeGroupAvatarUrl = prefs.getString(KEY_ACTIVE_GROUP_AVATAR_URL, null),
            latestPhotoId = prefs.getString(KEY_LATEST_PHOTO_ID, null),
            latestPhotoUrl = prefs.getString(KEY_LATEST_PHOTO_URL, null),
            latestPhotoCreatedAtIso = prefs.getString(KEY_LATEST_PHOTO_CREATED_AT_ISO, null),
            newReactionsCount = prefs.getInt(KEY_NEW_REACTIONS_COUNT, 0),
            cachedPhotoUrl = prefs.getString(KEY_CACHED_PHOTO_URL, null),
            latestContentKind = prefs.getString(KEY_LATEST_CONTENT_KIND, null),
            cachedPhotoPath = prefs.getString(KEY_CACHED_PHOTO_PATH, null)
        )
    }

    private fun prefs(context: Context) =
        context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
}

data class WidgetData(
    val activeGroupId: String?,
    val activeGroupName: String?,
    val activeGroupAvatarUrl: String?,
    val latestPhotoId: String?,
    val latestPhotoUrl: String?,
    val latestContentKind: String?,
    val latestPhotoCreatedAtIso: String?,
    val newReactionsCount: Int,
    val cachedPhotoUrl: String?,
    val cachedPhotoPath: String?
) {
    val hasActiveGroup: Boolean
        get() = !activeGroupId.isNullOrBlank()

    val hasPhoto: Boolean
        get() = !latestPhotoId.isNullOrBlank()

    val hasCachedPhoto: Boolean
        get() = !cachedPhotoPath.isNullOrBlank()

    val isLatestVideo: Boolean
        get() = latestContentKind == "video"
}