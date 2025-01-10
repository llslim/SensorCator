package com.tanujn45.a11y;

import android.content.Context;
import android.util.AttributeSet;
import android.view.LayoutInflater;
import android.view.ViewGroup;
import android.widget.ImageButton;
import android.widget.TextView;

import androidx.annotation.Nullable;
import androidx.constraintlayout.widget.ConstraintLayout;

public class AccItem extends ConstraintLayout {
    private TextView textView1, textView2, textView3;
    private ImageButton closeButton;

    public interface OnItemRemovedListener {
        void onItemRemoved(AccItem accItem);
    }

    private OnItemRemovedListener onItemRemovedListener;

    public void setOnItemRemovedListener(OnItemRemovedListener onItemRemovedListener) {
        this.onItemRemovedListener = onItemRemovedListener;
    }

    public AccItem(Context context) {
        super(context);
        init(context);
    }

    public AccItem(Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
        init(context);
    }

    public AccItem(Context context, @Nullable AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
        init(context);
    }

    private void init(Context context) {
        LayoutInflater inflator = LayoutInflater.from(context);
        inflator.inflate(R.layout.acc_item_view, this);

        textView2 = findViewById(R.id.textView2);
        textView1 = findViewById(R.id.textView1);
        textView3 = findViewById(R.id.textView3);
        closeButton = findViewById(R.id.closeButton);

        closeButton.setOnClickListener(v -> {
            ViewGroup parentView = (ViewGroup) getParent();
            if (parentView != null) {
                parentView.removeView(AccItem.this);
                if (onItemRemovedListener != null)
                    onItemRemovedListener.onItemRemoved(AccItem.this);
            }
        });
    }

    public void setText1(String text) {
        textView1.setText(text);
    }

    public String getText1() {
        return textView1.getText().toString();
    }

    public void setText2(String text) {
        textView2.setText(text);
    }

    public String getText2() {
        return textView2.getText().toString();
    }

    public void setText3(String text) {
        textView3.setText(text);
    }

    public String getText3() {
        return textView3.getText().toString();
    }

    public void setCloseButtonVisibility(boolean visible) {
        closeButton.setVisibility(visible ? VISIBLE : GONE);
    }
}
