package com.tanujn45.a11y.VideoTrimmer;

import static com.tanujn45.a11y.VideoTrimmer.utils.TrimVideoUtils.stringForTime;

import android.animation.ValueAnimator;
import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.media.MediaMetadataRetriever;
import android.media.MediaPlayer;
import android.net.Uri;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.os.Message;
import android.util.AttributeSet;
import android.util.Log;
import android.view.GestureDetector;
import android.view.LayoutInflater;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.RelativeLayout;
import android.widget.SeekBar;
import android.widget.TextView;
import android.widget.Toast;
import android.widget.VideoView;

import androidx.annotation.NonNull;
import androidx.constraintlayout.widget.ConstraintLayout;
import androidx.core.content.ContextCompat;

import com.github.mikephil.charting.charts.LineChart;
import com.github.mikephil.charting.data.Entry;
import com.github.mikephil.charting.data.LineData;
import com.github.mikephil.charting.data.LineDataSet;
import com.github.mikephil.charting.utils.ViewPortHandler;
import com.skydoves.powerspinner.DefaultSpinnerAdapter;
import com.skydoves.powerspinner.OnSpinnerItemSelectedListener;
import com.skydoves.powerspinner.PowerSpinnerView;
import com.tanujn45.a11y.CSVEditor.CSVFile;
import com.tanujn45.a11y.R;
import com.tanujn45.a11y.VideoTrimmer.interfaces.OnProgressVideoListener;
import com.tanujn45.a11y.VideoTrimmer.interfaces.OnRangeSeekBarListener;
import com.tanujn45.a11y.VideoTrimmer.interfaces.OnTrimVideoListener;
import com.tanujn45.a11y.VideoTrimmer.interfaces.VideoListener;
import com.tanujn45.a11y.VideoTrimmer.utils.BackgroundExecutor;
import com.tanujn45.a11y.VideoTrimmer.utils.TrimVideoUtils;
import com.tanujn45.a11y.VideoTrimmer.utils.UiThreadExecutor;
import com.tanujn45.a11y.VideoTrimmer.view.ProgressBarView;
import com.tanujn45.a11y.VideoTrimmer.view.RangeSeekBarView;
import com.tanujn45.a11y.VideoTrimmer.view.Thumb;
import com.tanujn45.a11y.VideoTrimmer.view.TimeLineView;

import java.io.File;
import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Objects;

public class VideoTrimmer extends FrameLayout {
    private static final String TAG = com.tanujn45.a11y.VideoTrimmer.VideoTrimmer.class.getSimpleName();
    private static final int MIN_TIME_FRAME = 1000;
    private static final int SHOW_PROGRESS = 2;
    private SeekBar mHolderTopView;
    private SeekBar mGraphHandler;
    private RangeSeekBarView mRangeSeekBarView;
    private RelativeLayout mLinearVideo;
    private View mTimeInfoContainer;
    private VideoView mVideoView;
    private ImageView mPlayView;
    private TextView mTextSize;
    private TextView mTextTimeFrame;
    private TextView mTextTime;
    private EditText startTime;
    private EditText endTime;
    private TimeLineView mTimeLineView;
    private LineChart lineChart;
    private PowerSpinnerView filterSpinner;
    private String currFilter;
    private ValueAnimator animator;
    private View graphBox;

    private ProgressBarView mVideoProgressIndicator;
    private Uri mSrc;
    private String mFinalPath;

    private int mMaxDuration;
    private List<OnProgressVideoListener> mListeners;

    private OnTrimVideoListener mOnTrimVideoListener;
    private VideoListener mVideoListener;
    private int mDuration = 0;
    private int mTimeVideo = 0;
    private int mStartPosition = 0;
    private int mEndPosition = 0;
    private int viewWidth = 0;
    private int totalDataPoints = 0;

    private long mOriginSizeFile;
    private boolean mResetSeekBar = true;
    private final MessageHandler mMessageHandler = new MessageHandler(Looper.getMainLooper(), this);


    public VideoTrimmer(@NonNull Context context, AttributeSet attrs) {
        this(context, attrs, 0);
    }

    public VideoTrimmer(@NonNull Context context, AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init(context);
    }

    private void init(Context context) {
        LayoutInflater.from(context).inflate(R.layout.view_time_line, this, true);

        mHolderTopView = findViewById(R.id.handlerTop);
        mGraphHandler = findViewById(R.id.graphHandler);
        mVideoProgressIndicator = findViewById(R.id.timeVideoView);
        mRangeSeekBarView = findViewById(R.id.timeLineBar);
        mLinearVideo = findViewById(R.id.layout_surface_view);
        mVideoView = findViewById(R.id.video_loader);
        mPlayView = findViewById(R.id.icon_video_play);
        mTimeInfoContainer = findViewById(R.id.timeText);
        mTextSize = findViewById(R.id.textSize);
        mTextTimeFrame = findViewById(R.id.textTimeSelection);
        mTextTime = findViewById(R.id.textTime);
        mTimeLineView = findViewById(R.id.timeLineView);
        lineChart = findViewById(R.id.lineChart);
        filterSpinner = findViewById(R.id.filterSpinner);
        startTime = findViewById(R.id.timeStartEditText);
        endTime = findViewById(R.id.timeEndEditText);
        graphBox = findViewById(R.id.graphBox);

        // testing the graph viewport
        lineChart.setViewPortOffsets(55f, 40f, 55f, 40f);

        setUpListeners();
        setUpMargins();
        initSpinner();
        adjustSeekBarThumbHeight();
    }

    private void getViewWidth() {
        //get view width of the mGraphHandler
        mGraphHandler.post(() -> viewWidth = mGraphHandler.getWidth());
        System.out.println("View Width: " + viewWidth);
    }

    @SuppressLint("ClickableViewAccessibility")
    private void setUpListeners() {
        mListeners = new ArrayList<>();
        mListeners.add((time, max, scale) -> updateVideoProgress(time));
        mListeners.add(mVideoProgressIndicator);

        findViewById(R.id.btCancel).setOnClickListener(view -> onCancelClicked());

        findViewById(R.id.btSave).setOnClickListener(view -> {
            try {
                onSaveClicked();
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        });

        final GestureDetector gestureDetector = new GestureDetector(getContext(), new GestureDetector.SimpleOnGestureListener() {
            @Override
            public boolean onSingleTapConfirmed(@NonNull MotionEvent e) {
                onClickVideoPlayPause();
                return true;
            }
        });

        mVideoView.setOnErrorListener((mediaPlayer, what, extra) -> {
            if (mOnTrimVideoListener != null)
                mOnTrimVideoListener.onError("Something went wrong reason : " + what);
            return false;
        });

        mVideoView.setOnTouchListener((v, event) -> {
            gestureDetector.onTouchEvent(event);
            return true;
        });

        mVideoView.setOnCompletionListener(mp -> {
            mPlayView.setVisibility(View.VISIBLE);
            mMessageHandler.removeMessages(SHOW_PROGRESS);
        });

        mRangeSeekBarView.addOnRangeSeekBarListener(new OnRangeSeekBarListener() {
            @Override
            public void onCreate(RangeSeekBarView rangeSeekBarView, int index, float value) {
                // Do nothing
            }

            @Override
            public void onSeek(RangeSeekBarView rangeSeekBarView, int index, float value) {
                onSeekThumbs(index, value);
            }

            @Override
            public void onSeekStart(RangeSeekBarView rangeSeekBarView, int index, float value) {
                // Do nothing
            }

            @Override
            public void onSeekStop(RangeSeekBarView rangeSeekBarView, int index, float value) {
                onStopSeekThumbs();
            }
        });
        mRangeSeekBarView.addOnRangeSeekBarListener(mVideoProgressIndicator);

        // this is to handle the graph
        mGraphHandler.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override
            public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                onPlayerIndicatorSeekChanged(progress, fromUser);
            }

            @Override
            public void onStartTrackingTouch(SeekBar seekBar) {
                onPlayerIndicatorSeekStart();
            }

            @Override
            public void onStopTrackingTouch(SeekBar seekBar) {
                onPlayerIndicatorSeekStop(seekBar);
            }
        });

        mHolderTopView.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override
            public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                onPlayerIndicatorSeekChanged(progress, fromUser);
            }

            @Override
            public void onStartTrackingTouch(SeekBar seekBar) {
                onPlayerIndicatorSeekStart();
            }

            @Override
            public void onStopTrackingTouch(SeekBar seekBar) {
                onPlayerIndicatorSeekStop(seekBar);
            }
        });

        mVideoView.setOnPreparedListener(this::onVideoPrepared);

        mVideoView.setOnCompletionListener(mp -> onVideoCompleted());
        //setGraph(mStartPosition, mEndPosition);
    }

    /*
    private void adjustSeekBarThumbHeight() {
        // Add a listener to adjust the SeekBar thumb height when the chart is laid out
        lineChart.getViewTreeObserver().addOnGlobalLayoutListener(() -> {
            // Get the height of the plot area
            ViewPortHandler viewPortHandler = lineChart.getViewPortHandler();
            float plotAreaHeight = viewPortHandler.getContentRect().height();
            viewWidth = (int) viewPortHandler.getContentRect().width();
            System.out.println("View Width: " + viewWidth);

            // Adjust the SeekBar thumb height
            Drawable thumbDrawable = ContextCompat.getDrawable(this.getContext(), R.drawable.graph_seekbar);
            if (thumbDrawable instanceof GradientDrawable) {
                GradientDrawable gradientDrawable = (GradientDrawable) thumbDrawable;
                gradientDrawable.setSize((int) dpToPx(3), (int) plotAreaHeight);
                mGraphHandler.setThumb(gradientDrawable);
            }
        });
    }
*/
    private void adjustSeekBarThumbHeight() {
        lineChart.post(() -> {
            // Get the height of the plot area after the layout has been properly measured
            ViewPortHandler viewPortHandler = lineChart.getViewPortHandler();
            if (viewPortHandler == null) {
                return; // Exit if the ViewPortHandler is not available
            }

            float plotAreaHeight = viewPortHandler.getContentRect().height();
            viewWidth = (int) viewPortHandler.getContentRect().width();
            System.out.println("View Width: " + viewWidth);

            // Adjust the viewBox height
            ConstraintLayout.LayoutParams params = (ConstraintLayout.LayoutParams) graphBox.getLayoutParams();
            params.height = (int) plotAreaHeight;
            graphBox.setLayoutParams(params);

            // Adjust the SeekBar thumb height
            Drawable thumbDrawable = ContextCompat.getDrawable(getContext(), R.drawable.graph_seekbar);
            if (thumbDrawable instanceof GradientDrawable) {
                GradientDrawable gradientDrawable = (GradientDrawable) thumbDrawable;
                int thumbWidth = (int) dpToPx(3);
                gradientDrawable.setSize(thumbWidth, (int) plotAreaHeight);
                mGraphHandler.setThumb(gradientDrawable);
            }
        });
    }


    private void initEditTextViews() {
        startTime.setText(String.valueOf(mStartPosition * totalDataPoints / mDuration));
        endTime.setText(String.valueOf(mEndPosition * totalDataPoints / mDuration));
        startTime.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_DONE) {
                hideKeyboard(getContext(), startTime);
                String text = startTime.getText().toString();
                if (text.isEmpty()) {
                    return true;
                }
                int time = Integer.parseInt(text);
                time = time * mDuration / totalDataPoints;
                if (time < 0) {
                    time = 0;
                } else if (time > mDuration) {
                    time = mDuration;
                } else if (time > mEndPosition) {
                    time = mEndPosition;
                }
                mStartPosition = time;
                mVideoView.seekTo(mStartPosition);
                setProgressBarPosition(mStartPosition);
                setTimeFrames();
                mRangeSeekBarView.setThumbValue(0, (mStartPosition * 100) / mDuration);
                mTimeVideo = mEndPosition - mStartPosition;
                startTime.setText(String.valueOf(mStartPosition * totalDataPoints / mDuration));
                updateViewBox();
                return true;
            }
            return false;
        });

        endTime.setOnEditorActionListener((v, actionId, event) -> {
            if (actionId == EditorInfo.IME_ACTION_DONE) {
                hideKeyboard(getContext(), endTime);
                String text = endTime.getText().toString();
                if (text.isEmpty()) {
                    return true;
                }
                int time = Integer.parseInt(text);
                time = time * mDuration / totalDataPoints;
                if (time < 0) {
                    time = 0;
                } else if (time > mDuration) {
                    time = mDuration;
                } else if (time < mStartPosition) {
                    time = mStartPosition;
                }
                mEndPosition = time;
                setProgressBarPosition(mEndPosition);
                setTimeFrames();
                mRangeSeekBarView.setThumbValue(1, (mEndPosition * 100) / mDuration);
                mTimeVideo = mEndPosition - mStartPosition;
                endTime.setText(String.valueOf(mEndPosition * totalDataPoints / mDuration));
                updateViewBox();
                return true;
            }
            return false;
        });
    }

    public static void hideKeyboard(Context context, View view) {
        InputMethodManager imm = (InputMethodManager) context.getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null) {
            imm.hideSoftInputFromWindow(view.getWindowToken(), InputMethodManager.HIDE_NOT_ALWAYS);
        }
    }

    private float dpToPx(float dp) {
        return dp * getResources().getDisplayMetrics().density;
    }

    private void initSpinner() {
        currFilter = "acc";
        DefaultSpinnerAdapter adapterFilter = new DefaultSpinnerAdapter(filterSpinner);
        List<CharSequence> items = Arrays.asList("Acc", "Acc MA", "Gyro");
        adapterFilter.setItems(items);
        filterSpinner.setSpinnerAdapter(adapterFilter);
        filterSpinner.selectItemByIndex(0);

        filterSpinner.setOnSpinnerItemSelectedListener((OnSpinnerItemSelectedListener<String>) (oldIndex, oldItem, newIndex, newItem) -> {
            newItem = newItem.toLowerCase().replace(" ", "_");
            currFilter = newItem;
            setGraph(mStartPosition, mEndPosition);
        });
    }

    private void setUpMargins() {
        int marge = mRangeSeekBarView.getThumbs().get(0).getWidthBitmap();
        int widthSeek = mHolderTopView.getThumb().getMinimumWidth() / 2;

        RelativeLayout.LayoutParams lp = (RelativeLayout.LayoutParams) mHolderTopView.getLayoutParams();
        lp.setMargins(marge - widthSeek, 0, marge - widthSeek, 0);
        mHolderTopView.setLayoutParams(lp);

        lp = (RelativeLayout.LayoutParams) mTimeLineView.getLayoutParams();
        lp.setMargins(marge, 0, marge, 0);
        mTimeLineView.setLayoutParams(lp);

        lp = (RelativeLayout.LayoutParams) mVideoProgressIndicator.getLayoutParams();
        lp.setMargins(marge, 0, marge, 0);
        mVideoProgressIndicator.setLayoutParams(lp);
    }

    private void onSaveClicked() throws Exception {
        if (mStartPosition <= 0 && mEndPosition >= mDuration) {
            mStartPosition = 0;
            mEndPosition = mDuration;
//            if (mOnTrimVideoListener != null)
//                mOnTrimVideoListener.getResult(mSrc, mStartPosition, mEndPosition);
        }

//        else {
        mPlayView.setVisibility(View.VISIBLE);
        mVideoView.pause();

        MediaMetadataRetriever mediaMetadataRetriever = new MediaMetadataRetriever();
        mediaMetadataRetriever.setDataSource(getContext(), mSrc);
        long METADATA_KEY_DURATION = Long.parseLong(mediaMetadataRetriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION));

        final File file = new File(Objects.requireNonNull(mSrc.getPath()));

        if (mTimeVideo < MIN_TIME_FRAME) {
            if ((METADATA_KEY_DURATION - mEndPosition) > (MIN_TIME_FRAME - mTimeVideo)) {
                mEndPosition += (MIN_TIME_FRAME - mTimeVideo);
            } else if (mStartPosition > (MIN_TIME_FRAME - mTimeVideo)) {
                mStartPosition -= (MIN_TIME_FRAME - mTimeVideo);
            }
        }

        //notify that video trimming started
        if (mOnTrimVideoListener != null) mOnTrimVideoListener.onTrimStarted();

        BackgroundExecutor.execute(new BackgroundExecutor.Task("", 0L, "") {
            @Override
            public void execute() {
                try {
                    TrimVideoUtils.startTrim(file, getDestinationPath(), mStartPosition, mEndPosition, mOnTrimVideoListener);
                } catch (final Throwable e) {
                    // Use a Handler to show Toast on the main thread
                    new Handler(Looper.getMainLooper()).post(() -> {
                        String errorMessage = "Unable to trim video. File may be corrupt or too short.";
                        Toast.makeText(getContext(), errorMessage, Toast.LENGTH_LONG).show();
                    });
                }
            }
        });
//        }
    }

    public int getStartPosition() {
        return mStartPosition;
    }

    public int getEndPosition() {
        return mEndPosition;
    }

    private void onClickVideoPlayPause() {
        if (mVideoView.isPlaying()) {
            mPlayView.setVisibility(View.VISIBLE);
            mMessageHandler.removeMessages(SHOW_PROGRESS);
            mVideoView.pause();
        } else {
            mPlayView.setVisibility(View.GONE);

            if (mResetSeekBar) {
                mResetSeekBar = false;
                mVideoView.seekTo(mStartPosition);
            }

            mMessageHandler.sendEmptyMessage(SHOW_PROGRESS);
            mVideoView.start();
        }
    }

    private void onCancelClicked() {
        mVideoView.stopPlayback();
        if (mOnTrimVideoListener != null) {
            mOnTrimVideoListener.cancelAction();
        }
    }

    private String getDestinationPath() {
        if (mFinalPath == null) {
            File folder = Environment.getExternalStorageDirectory();
            mFinalPath = folder.getPath() + File.separator;
            Log.d(TAG, "Using default path " + mFinalPath);
        }
        return mFinalPath;
    }

    private void onPlayerIndicatorSeekChanged(int progress, boolean fromUser) {

        int duration = (int) ((mDuration * progress) / 1000L);

        if (fromUser) {
            if (duration < mStartPosition) {
                setProgressBarPosition(mStartPosition);
                duration = mStartPosition;
            } else if (duration > mEndPosition) {
                setProgressBarPosition(mEndPosition);
                duration = mEndPosition;
            } else {
                setProgressBarPosition(duration);
            }
            setTimeVideo(duration);
        }
    }

    private void onPlayerIndicatorSeekStart() {
        mMessageHandler.removeMessages(SHOW_PROGRESS);
        mVideoView.pause();
        mPlayView.setVisibility(View.VISIBLE);
        notifyProgressUpdate(false);
    }

    private void onPlayerIndicatorSeekStop(@NonNull SeekBar seekBar) {
        mMessageHandler.removeMessages(SHOW_PROGRESS);
        mVideoView.pause();
        mPlayView.setVisibility(View.VISIBLE);

        int duration = (int) ((mDuration * seekBar.getProgress()) / 1000L);
        mVideoView.seekTo(duration);
        setTimeVideo(duration);
        notifyProgressUpdate(false);

        setProgressBarPosition(duration);
    }

    private void onVideoPrepared(@NonNull MediaPlayer mp) {
        // Adjust the size of the video
        // so it fits on the screen
        int videoWidth = mp.getVideoWidth();
        int videoHeight = mp.getVideoHeight();
        float videoProportion = (float) videoWidth / (float) videoHeight;
        int screenWidth = mLinearVideo.getWidth();
        int screenHeight = mLinearVideo.getHeight();
        float screenProportion = (float) screenWidth / (float) screenHeight;
        ViewGroup.LayoutParams lp = mVideoView.getLayoutParams();

        if (videoProportion > screenProportion) {
            lp.width = screenWidth;
            lp.height = (int) ((float) screenWidth / videoProportion);
        } else {
            lp.width = (int) (videoProportion * (float) screenHeight);
            lp.height = screenHeight;
        }
        mVideoView.setLayoutParams(lp);

        mPlayView.setVisibility(View.VISIBLE);

        mDuration = mVideoView.getDuration();
        setSeekBarPosition();

        setTimeFrames();
        setTimeVideo(0);

        if (mVideoListener != null) {
            mVideoListener.onVideoPrepared();
        }

        updateViewBox();
        setGraph(mStartPosition, mEndPosition);
        if (mDuration > 0) {
            startTime.setText(String.valueOf(mStartPosition * totalDataPoints / mDuration));
            endTime.setText(String.valueOf(mEndPosition * totalDataPoints / mDuration));
        }
        initEditTextViews();
    }

    private void setSeekBarPosition() {

        if (mDuration >= mMaxDuration) {
            mStartPosition = mDuration / 2 - mMaxDuration / 2;
            mEndPosition = mDuration / 2 + mMaxDuration / 2;

            mRangeSeekBarView.setThumbValue(0, (mStartPosition * 100) / mDuration);
            mRangeSeekBarView.setThumbValue(1, (mEndPosition * 100) / mDuration);

        } else {
            mStartPosition = 0;
            mEndPosition = mDuration;
        }

        setProgressBarPosition(mStartPosition);
        mVideoView.seekTo(mStartPosition);

        mTimeVideo = mDuration;
        mRangeSeekBarView.initMaxWidth();
    }

    private void setTimeFrames() {
        String seconds = getContext().getString(R.string.short_seconds);
        mTextTimeFrame.setText(String.format("%s %s - %s %s", stringForTime(mStartPosition), seconds, stringForTime(mEndPosition), seconds));
    }

    private void setTimeVideo(int position) {
        String seconds = getContext().getString(R.string.short_seconds);
        mTextTime.setText(String.format("%s %s", stringForTime(position), seconds));
    }

    private void onSeekThumbs(int index, float value) {
        switch (index) {
            case Thumb.LEFT: {
                mStartPosition = (int) ((mDuration * value) / 100L);
                mVideoView.seekTo(mStartPosition);
                if (mDuration > 0) {
                    startTime.setText(String.valueOf(mStartPosition * totalDataPoints / mDuration));
                }
                break;
            }
            case Thumb.RIGHT: {
                mEndPosition = (int) ((mDuration * value) / 100L);
                if (mDuration > 0) {
                    endTime.setText(String.valueOf(mEndPosition * totalDataPoints / mDuration));
                }
                break;
            }
        }

        updateViewBox();

        setProgressBarPosition(mStartPosition);
        // Change here
        // mGraphHandler.setProgress(0);

        setTimeFrames();
        mTimeVideo = mEndPosition - mStartPosition;
    }

    private void updateViewBox() {
        float viewStartPosition = (float) mStartPosition / mDuration * viewWidth;
        ConstraintLayout.LayoutParams params = (ConstraintLayout.LayoutParams) graphBox.getLayoutParams();
        params.setMarginStart((int) viewStartPosition + (int) dpToPx(20));

//        System.out.println("Start position" + mStartPosition + " End position" + mEndPosition + " Duration" + mDuration);
        float viewEndPosition = (float) (mEndPosition - mStartPosition) / mDuration * viewWidth;
        System.out.println("View End Position: " + viewEndPosition);
        if (viewEndPosition <= 0) {
            viewEndPosition = 1;
        }
        params.width = (int) viewEndPosition;

        graphBox.setLayoutParams(params);
    }

    private void setGraph(int mStartPosition, int mEndPosition) {
        // mStartPosition is the start position
        // mEndPosition is the end position
        // mSrc is the video uri

        float start = (float) mStartPosition / 1000;
        float end = (float) mEndPosition / 1000;

        if (mSrc == null) {
            return;
        }
        String path = mSrc.getPath();
        assert path != null;
        path = path.replace(".mp4", ".csv").replace("Videos", "Data");

        try {
            CSVFile csvFile = new CSVFile(path);
            if (!csvFile.checkIfHeaderExists("Time")) {
                csvFile.applyTime();
            }
            if (!csvFile.checkIfHeaderExists("acc_ma_x")) {
                csvFile.applyMovingAverage();
            }

            List<String> accX = csvFile.getColumnData(currFilter + "_x");
            List<String> accY = csvFile.getColumnData(currFilter + "_y");
            List<String> accZ = csvFile.getColumnData(currFilter + "_z");

            List<Entry> entriesX = new ArrayList<>();
            List<Entry> entriesY = new ArrayList<>();
            List<Entry> entriesZ = new ArrayList<>();

            for (int i = 0; i < accX.size(); i++) {
                entriesX.add(new Entry(i, Float.parseFloat(accX.get(i))));
                entriesY.add(new Entry(i, Float.parseFloat(accY.get(i))));
                entriesZ.add(new Entry(i, Float.parseFloat(accZ.get(i))));
            }

            LineDataSet dataSetX = new LineDataSet(entriesX, "Acc X");
            dataSetX.setColor(Color.RED);
            dataSetX.setLineWidth(1.5f);
            dataSetX.setDrawCircles(false);

            LineDataSet dataSetY = new LineDataSet(entriesY, "Acc Y");
            dataSetY.setColor(Color.GREEN);
            dataSetY.setLineWidth(1.5f);
            dataSetY.setDrawCircles(false);

            LineDataSet dataSetZ = new LineDataSet(entriesZ, "Acc Z");
            dataSetZ.setColor(Color.BLUE);
            dataSetZ.setLineWidth(1.5f);
            dataSetZ.setDrawCircles(false);

            totalDataPoints = accX.size();

            LineData lineData = new LineData(dataSetX, dataSetY, dataSetZ);

            lineChart.setData(lineData);
            lineChart.getDescription().setEnabled(false);
            lineChart.setDrawMarkers(false);
            lineChart.invalidate();
        } catch (Exception e) {
            e.printStackTrace();
        }
    }

    private void onStopSeekThumbs() {
        mMessageHandler.removeMessages(SHOW_PROGRESS);
        mVideoView.pause();
        mPlayView.setVisibility(View.VISIBLE);
    }

    private void onVideoCompleted() {
        mVideoView.seekTo(mStartPosition);
    }

    private void notifyProgressUpdate(boolean all) {
        if (mDuration == 0) return;

        int position = mVideoView.getCurrentPosition();
        if (all) {
            for (OnProgressVideoListener item : mListeners) {
                item.updateProgress(position, mDuration, ((position * 100) / mDuration));
            }
        } else {
            mListeners.get(1).updateProgress(position, mDuration, ((position * 100) / mDuration));
        }
    }

    private void updateVideoProgress(int time) {
        if (mVideoView == null) {
            return;
        }

        System.out.println("Time: " + time + " mEndPosition: " + mEndPosition);

        if (time >= mEndPosition) {
            mMessageHandler.removeMessages(SHOW_PROGRESS);
            mVideoView.pause();
            mPlayView.setVisibility(View.VISIBLE);
            mResetSeekBar = true;
            return;
        }

        if (mHolderTopView != null && mGraphHandler != null) {
            setProgressBarPosition(time);
        }

        setTimeVideo(time);
    }

    private void setProgressBarPosition(int position) {
        if (mDuration > 0) {
            long pos = 1000L * position / mDuration;
            mHolderTopView.setProgress((int) pos);
            mGraphHandler.setProgress((int) pos);

            // if (mTimeVideo != 0) {
            //     long graphPos = 1000L * (position - mStartPosition) / mTimeVideo;
            //     mGraphHandler.setProgress((int) graphPos);
            // }
        }
    }


    /**
     * Set video information visibility.
     * For now this is for debugging
     *
     * @param visible whether or not the videoInformation will be visible
     */
    public void setVideoInformationVisibility(boolean visible) {
        mTimeInfoContainer.setVisibility(visible ? VISIBLE : GONE);
    }

    /**
     * Listener for events such as trimming operation success and cancel
     *
     * @param onTrimVideoListener interface for events
     */
    @SuppressWarnings("unused")
    public void setOnTrimVideoListener(OnTrimVideoListener onTrimVideoListener) {
        mOnTrimVideoListener = onTrimVideoListener;
    }

    /**
     * Listener for some {@link VideoView} events
     *
     * @param onK4LVideoListener interface for events
     */
    @SuppressWarnings("unused")
    public void setOnK4LVideoListener(VideoListener onK4LVideoListener) {
        mVideoListener = onK4LVideoListener;
    }

    /**
     * Sets the path where the trimmed video will be saved
     * Ex: /storage/emulated/0/MyAppFolder/
     *
     * @param finalPath the full path
     */
    @SuppressWarnings("unused")
    public void setDestinationPath(final String finalPath) {
        mFinalPath = finalPath;
        Log.d(TAG, "Setting custom path " + mFinalPath);
    }

    /**
     * Cancel all current operations
     */
    public void destroy() {
        BackgroundExecutor.cancelAll("", true);
        UiThreadExecutor.cancelAll("");
    }

    /**
     * Set the maximum duration of the trimmed video.
     * The trimmer interface wont allow the user to set duration longer than maxDuration
     *
     * @param maxDuration the maximum duration of the trimmed video in seconds
     */
    @SuppressWarnings("unused")
    public void setMaxDuration(int maxDuration) {
        mMaxDuration = maxDuration * 1000;
    }

    /**
     * Sets the uri of the video to be trimmer
     *
     * @param videoURI Uri of the video
     */
    @SuppressWarnings("unused")
    public void setVideoURI(final Uri videoURI) {
        mSrc = videoURI;

        if (mOriginSizeFile == 0) {
            File file = new File(mSrc.getPath());

            mOriginSizeFile = file.length();
            long fileSizeInKB = mOriginSizeFile / 1024;

            if (fileSizeInKB > 1000) {
                long fileSizeInMB = fileSizeInKB / 1024;
                mTextSize.setText(String.format("%s %s", fileSizeInMB, getContext().getString(R.string.megabyte)));
            } else {
                mTextSize.setText(String.format("%s %s", fileSizeInKB, getContext().getString(R.string.kilobyte)));
            }
        }

        mVideoView.setVideoURI(mSrc);
        mVideoView.requestFocus();

        mTimeLineView.setVideo(mSrc);

        // Set the graph (changed here)
        // setGraph(mStartPosition, mEndPosition);
        if (mDuration > 0) {
            startTime.setText(String.valueOf(mStartPosition * totalDataPoints / mDuration));
            endTime.setText(String.valueOf(mEndPosition * totalDataPoints / mDuration));
        }
    }

    private static class MessageHandler extends Handler {

        @NonNull
        private final WeakReference<com.tanujn45.a11y.VideoTrimmer.VideoTrimmer> mView;

        MessageHandler(@NonNull Looper looper, com.tanujn45.a11y.VideoTrimmer.VideoTrimmer view) {
            super(looper);
            mView = new WeakReference<>(view);
        }

        @Override
        public void handleMessage(Message msg) {
            com.tanujn45.a11y.VideoTrimmer.VideoTrimmer view = mView.get();
            if (view == null || view.mVideoView == null) {
                return;
            }

            view.notifyProgressUpdate(true);
            if (view.mVideoView.isPlaying()) {
                sendEmptyMessageDelayed(0, 10);
            }
        }
    }

}


