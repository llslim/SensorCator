package com.tanujn45.a11y.VideoTrimmer.interfaces;

import com.tanujn45.a11y.VideoTrimmer.view.RangeSeekBarView;

public interface OnRangeSeekBarListener {
    void onCreate(RangeSeekBarView rangeSeekBarView, int index, float value);

    void onSeek(RangeSeekBarView rangeSeekBarView, int index, float value);

    void onSeekStart(RangeSeekBarView rangeSeekBarView, int index, float value);

    void onSeekStop(RangeSeekBarView rangeSeekBarView, int index, float value);
}
