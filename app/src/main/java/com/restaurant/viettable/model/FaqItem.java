package com.restaurant.viettable.model;

import java.io.Serializable;

public final class FaqItem implements Serializable {
    private final String faqId;
    private final String question;
    private final String answer;

    public FaqItem(String faqId, String question, String answer) {
        this.faqId = faqId;
        this.question = question;
        this.answer = answer;
    }

    public String getFaqId() { return faqId; }
    public String getQuestion() { return question; }
    public String getAnswer() { return answer; }
}
