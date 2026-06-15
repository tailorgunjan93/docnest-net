# DocNest .NET — multi-format RAG accuracy eval (Python parity)
_Generated 2026-06-14 17:16 UTC · 10 documents, 88 questions · judge: LLM (openai-compatible/qwen2.5:7b) (hit = score ≥ 7)_
_Mode: **LLM-assisted** (Layers 2-4 enabled via gpt-oss-120b)_
_Retrieval: **hybrid** — BM25 + dense (sentence-transformers/all-MiniLM-L6-v2) + rerank (cross-encoder/ms-marco-MiniLM-L-6-v2)_

## Phase 2 — real PDFs (IPCC · BIS · GPT-3 · Attention · Llama 2 · Constitutional AI)

| File | Question | Layer | Tokens | Score | Answer |
|---|---|---|---|---|---|
| ipcc_spm.pdf | What is the observed increase in global surface temperatur… | 2 | 147 | 9/10 | The observed increase is **1.09 °C** (with a confidence interval of **… |
| ipcc_spm.pdf | What is the remaining global carbon budget for limiting wa… | 1 | 0 | 8/10 | Summary for Policymakers B.5.3 If the annual CO2 emissions between 202… |
| ipcc_spm.pdf | What does the report say about limiting warming to 1.5°C —… | 2 | 497 | 8/10 | The report states that limiting warming to 1.5 °C hinges on cumulative… |
| ipcc_spm.pdf | What are the projected sea level rise ranges mentioned in … | 2 | 463 | 6/10 | The report gives the following projected global‑mean sea‑level rise ra… |
| ipcc_spm.pdf | How does the report link climate change to extreme weather… | 2 | 222 | 6/10 | The report states that observed climate change has intensified hot ext… |
| bis_2024.pdf | What does the BIS 2024 report say about the disinflation p… | 2 | 606 | 8/10 | The BIS 2024 report notes that **global inflation has been steadily mo… |
| bis_2024.pdf | What does the BIS 2024 report say about central bank polic… | 2 | 501 | 6/10 | The BIS 2024 report notes that, when inflation spikes, central banks r… |
| bis_2024.pdf | What specific financial stability risks does the BIS highl… | 1 | 0 | 0/10 | The business model of life insurance companies (ICs), which have tradi… |
| bis_2024.pdf | What financial stability risks does the BIS identify relat… | 3 | 1476 | 8/10 | **Financial‑stability risks flagged by the BIS**  | Theme | How the BI… |
| bis_2024.pdf | What fiscal pressure points does the BIS 2024 report ident… | 0 | 0 | 8/10 | Fiscal pressure points: 22 (source: §3.2.1.1.1) |
| gpt3_paper.pdf | What are ALL the parameter sizes of GPT-3 models presented… | 3 | 357 | 8/10 | **GPT‑3 parameter‑size variants mentioned in the paper (smallest → lar… |
| gpt3_paper.pdf | What are the training corpora (Common Crawl, WebText2, Boo… | 4 | 322 | 0/10 | The excerpts you provided do not contain any information about the rel… |
| gpt3_paper.pdf | How does GPT-3's architecture compare to GPT-2? What are t… | 4 | 588 | 8/10 | **What the supplied excerpts tell us about the architecture of the lar… |
| gpt3_paper.pdf | How many in-context examples are used in GPT-3's zero-shot… | 3 | 218 | 8/10 | The excerpts provided do not specify the exact number of in‑context ex… |
| gpt3_paper.pdf | What limitations and risks does the GPT-3 paper acknowledg… | 2 | 235 | 6/10 | The GPT‑3 paper notes several concrete limitations:  * **Text‑generati… |
| attention_paper.pdf | How many attention heads does the Transformer base model u… | 3 | 237 | 8/10 | **Encoder / Decoder depth**   The document states that both the encode… |
| attention_paper.pdf | What EN-DE BLEU scores do the Transformer base and big mod… | 0 | 0 | 8/10 | Transformer big: 28.441 (source: §24.5) |
| attention_paper.pdf | What training data and hardware were used to train the Tra… | 3 | 346 | 9/10 | **Training data**  * **English → German** – the standard WMT 2014 Engl… |
| attention_paper.pdf | What are the per-layer computational complexities of self-… | 3 | 339 | 8/10 | **Per‑layer computational complexities (Table 1)**    | Layer type | P… |
| attention_paper.pdf | What regularization techniques are applied during Transfor… | 3 | 235 | 8/10 | **Regularization techniques mentioned**  The method description for th… |
| llama2_paper.pdf | What are ALL the parameter sizes of the Llama 2 model fami… | 4 | 347 | 8/10 | **Parameter sizes mentioned for the Llama 2 model family**  - **7 bill… |
| llama2_paper.pdf | How does Llama 2-Chat 70B compare to ChatGPT (GPT-3.5) on … | 4 | 290 | 8/10 | The provided excerpts do not contain any direct human‑preference evalu… |
| llama2_paper.pdf | What context length do Llama 2 models support, and how was… | 3 | 276 | 9/10 | **Context length**  - Llama 2 models are built to handle **4 096 token… |
| llama2_paper.pdf | How were the reward models for Llama 2-Chat RLHF trained, … | 1 | 0 | 0/10 | Here,wediscusstheinterestingpropertieswehaveobservedwithRLHF(Section5.… |
| llama2_paper.pdf | What is RLHF Ghost Attention (GAtt) and what problem does … | 2 | 111 | 6/10 | GhostAttention (GAtt) is a new technique introduced in the paper. It i… |
| constitutional_ai.pdf | What is the core idea behind Constitutional AI and what pr… | 3 | 621 | 9/10 | **Core idea of Constitutional AI (CAI)**   Constitutional AI is a trai… |
| constitutional_ai.pdf | What two training phases make up the Constitutional AI met… | 2 | 126 | 8/10 | The Constitutional AI method consists of two training phases:    1. **… |
| constitutional_ai.pdf | How does the paper evaluate harmlessness vs helpfulness, a… | 3 | 868 | 9/10 | **How the paper evaluates “harmlessness vs helpfulness”**  | Step | Wh… |
| constitutional_ai.pdf | What is the 'critique and revision' step in Constitutional… | 2 | 244 | 8/10 | The “critique and revision” step is the part of the Constitutional AI … |
| constitutional_ai.pdf | What does the Constitutional AI paper show about whether A… | 3 | 578 | 9/10 | **Answer**  The Constitutional AI (CAI) paper provides empirical evide… |

| File | Avg score | Hit-rate (≥7) |
|---|---|---|
| ipcc_spm.pdf | 7.4/10 | 60% |
| bis_2024.pdf | 6.0/10 | 60% |
| gpt3_paper.pdf | 6.0/10 | 60% |
| attention_paper.pdf | 8.2/10 | 100% |
| llama2_paper.pdf | 6.2/10 | 60% |
| constitutional_ai.pdf | 8.6/10 | 100% |
| **Phase 2 overall** | **7.1/10** | **73%** |

_30 questions, 10250 LLM tokens._

## Overall

| Phase | Avg score | Hit-rate (≥7) |
|---|---|---|
| Phase 2 — PDFs | 7.1/10 | 73% |
| **All (30 Qs)** | **7.1/10** | — |

