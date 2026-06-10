# DocNest .NET — multi-format RAG accuracy eval (Python parity)
_Generated 2026-06-10 06:03 UTC · 10 documents, 88 questions · judge: local (number ±6% + keyword + phrase overlap, hit = score ≥ 7)_
_Mode: **LLM-assisted** (Layers 2-4 enabled via openai/gpt-oss-120b)_

## Phase 1 — generated files (xlsx · docx · html · md)

| File | Question | Layer | Tokens | Score | Answer |
|---|---|---|---|---|---|
| acme_financial.xlsx | What was the total Q1 revenue across all products in USD t… | 2 | 206 | 10/10 | The total Q1 revenue across all products was **12,550 USD thousands**. |
| acme_financial.xlsx | Which product had the highest annual revenue and how much … | 1 | 0 | 10/10 | Sheet: Revenue Columns: Product, Q1, Q2, Q3, Q4, Annual Total Data: Da… |
| acme_financial.xlsx | How many total employees does the Engineering department h… | 1 | 0 | 10/10 | Sheet: Headcount Columns: Department, Full-Time, Part-Time, Total Data… |
| acme_financial.xlsx | Which region had the highest year-over-year growth percent… | 2 | 178 | 10/10 | Asia Pacific had the highest year‑over‑year growth percentage (31.7%). |
| acme_financial.xlsx | What is the NRR% for the Mid-Market customer segment? | 1 | 0 | 10/10 | Sheet: Customer Segments Columns: Segment, Customers, Revenue, ARPU, N… |
| acme_financial.xlsx | What was SupportDesk's Q3 revenue and what was its full-ye… | 3 | 310 | 10/10 | **SupportDesk**  - **Q3 revenue:** **1,100 (USD thousands)** - **Full‑… |
| acme_financial.xlsx | Which month had the highest combined total revenue across … | 2 | 382 | 0/10 | November. |
| acme_financial.xlsx | What is the total ARR (in USD thousands) from Enterprise t… | 1 | 0 | 0/10 | Sheet: Top Accounts Columns: Account, Tier, Region, ARR ($k), Products… |
| acme_financial.xlsx | How many customer accounts are based in the Asia Pacific r… | 1 | 0 | 0/10 | Sheet: Top Accounts Columns: Account, Tier, Region, ARR ($k), Products… |
| acme_financial.xlsx | Which product had the highest annual gross margin percenta… | 2 | 186 | 7/10 | DataSync Pro. |
| acme_financial.xlsx | What is the Net Revenue Retention (NRR) rate for Enterpris… | 1 | 0 | 10/10 | Sheet: Customer Segments Columns: Segment, Customers, Revenue, ARPU, N… |
| acme_financial.xlsx | What was the total annual R&D expense and how does it comp… | 2 | 235 | 10/10 | The total annual R&D expense was **$210,000**.   The Sales & Marketing… |
| acme_financial.xlsx | What was the total partnership channel revenue in 2024 and… | 2 | 203 | 10/10 | The total partnership channel revenue was **38,700**, and the **Referr… |
| acme_financial.xlsx | How many R&D projects have Priority P0 status and how many… | 1 | 0 | 10/10 | Sheet: R&D Projects Columns: Project, Team, Budget ($k), Spent ($k), S… |
| acme_financial.xlsx | What is the total revenue and YoY growth percentage for th… | 1 | 0 | 10/10 | Sheet: Partnerships Columns: Partner Type, Partners, Revenue, Rev Shar… |
| techvision_annual.docx | What was TechVision's total revenue in FY2024 and the year… | 3 | 208 | 0/10 | I’m sorry, but the excerpts you provided don’t include any figures for… |
| techvision_annual.docx | Which business segment became the largest revenue contribu… | 1 | 0 | 6/10 | Revenue grew across all three business segments, with cloud services b… |
| techvision_annual.docx | What is the company's net income margin in FY2024? | 3 | 190 | 6/10 | **Net‑income margin (FY 2024)**    The FY 2024 filing does not give a … |
| techvision_annual.docx | What is the severity rating of the cybersecurity breach ri… | 2 | 136 | 10/10 | HIGH |
| techvision_annual.docx | What is TechVision's FY2025 revenue guidance range? | 3 | 127 | 8/10 | TechVision’s FY2025 revenue guidance is **$1.55 billion to $1.62 billi… |
| techvision_annual.docx | What percentage of TechVision's electricity came from rene… | 3 | 203 | 8/10 | In 2024, **94 % of TechVision’s global electricity consumption came fr… |
| techvision_annual.docx | What is the estimated cost of the Zero-Trust Security Suit… | 3 | 189 | 0/10 | I’m sorry, but the excerpt you provided for the product roadmap (secti… |
| techvision_annual.docx | What is TechVision's estimated serviceable obtainable mark… | 0 | 0 | 10/10 | estimated serviceable obtainable market: $12 billion USD (source: §9) |
| techvision_annual.docx | Which three countries are TechVision's priority internatio… | 1 | 0 | 10/10 | Table: FY2025 International Expansion Targets  [Figure 3: Global Reven… |
| techvision_annual.docx | What is TechVision's platform uptime and mean time to reco… | 3 | 154 | 7/10 | I’m happy to help you calculate TechVision’s platform uptime and mean‑… |
| techvision_annual.docx | What are the names and acquisition prices of the two compa… | 3 | 185 | 0/10 | Based on the excerpts you provided—§12.2 (litigation and insurance det… |
| techvision_annual.docx | What is TechVision's LTV:CAC ratio and how does it compare… | 3 | 198 | 7/10 | **Answer Overview**  To calculate TechVision’s **LTV : CAC** (Lifetime… |
| techvision_annual.docx | What is TechVision's Net Revenue Retention (NRR) rate in F… | 3 | 223 | 0/10 | Based on the excerpts you provided, none of the sections contain a fig… |
| techvision_annual.docx | How many active compliance certifications does TechVision … | 2 | 157 | 10/10 | TechVision holds **6 active compliance certifications**, and the certi… |
| nexusapi_docs.html | What are the rate limits and price for the Pro plan? | 2 | 98 | 10/10 | **Pro plan**   - **Requests/minute:** 1,000   - **Requests/day:** 500,… |
| nexusapi_docs.html | How long do OAuth tokens last and how long are refresh tok… | 0 | 0 | 7/10 | Refresh tokens valid: 30 days (source: §1.1) |
| nexusapi_docs.html | What HTTP method and endpoint is used to trigger AI parsin… | 3 | 231 | 5/10 | The excerpts you shared don’t include any details about the endpoint o… |
| nexusapi_docs.html | What happens if a webhook endpoint does not respond within… | 0 | 0 | 3/10 | within: 10 seconds (source: §1.4) |
| nexusapi_docs.html | What is HTTP error code 429 in NexusAPI called, what cause… | 3 | 274 | 10/10 | **HTTP 429 – “Too Many Requests”**  | Aspect | Details (as defined for… |
| nexusapi_docs.html | What are the SDK package names and minimum runtime require… | 2 | 117 | 10/10 | - **Python** – Package: **nexus-sdk**; Minimum runtime: **Python 3.9+*… |
| nexusapi_docs.html | What is the base URL for NexusAPI v3 and what version is d… | 0 | 0 | 3/10 | Version: 3.4 (source: §1) |
| nexusapi_docs.html | What HTTP method and endpoint retrieves a paginated list o… | 3 | 223 | 9/10 | The sections you provided describe billing‑related calls ( §1.9 ) and … |
| nexusapi_docs.html | On which plan are API keys for server-to-server communicat… | 1 | 0 | 10/10 | NexusAPI uses OAuth 2.0 Bearer tokens for all API calls. API keys for … |
| nexusapi_docs.html | What is the daily request quota, burst limit, and monthly … | 3 | 192 | 6/10 | **Enterprise Plan – Key Limits & Pricing**  | Item | Value | Source | … |
| nexusapi_docs.html | How long is raw analytics event data retained for Pro plan… | 0 | 0 | 8/10 | Raw event data retained: 90 days (source: §1.6.1) |
| nexusapi_docs.html | What permission role is required to manage workspace user … | 1 | 0 | 10/10 | Manage workspace members, teams, and granular permission sets. The min… |
| nexusapi_docs.html | When did the NexusAPI v2 reach end-of-life, and how long i… | 0 | 0 | 0/10 | Version: 3.4 (source: §1) |
| nexusapi_docs.html | What are the available geographic regions for Enterprise d… | 1 | 0 | 10/10 | Data residency options allow Enterprise customers to restrict storage … |
| cloudmesh_spec.md | What is CloudMesh's monthly data processing volume and upt… | 2 | 90 | 10/10 | CloudMesh processes **2.4 petabytes of data per month**, and its core … |
| cloudmesh_spec.md | What encryption standard is used for data at rest and how … | 3 | 220 | 9/10 | The system encrypts all data at rest using **AES‑256‑GCM**.   Key rota… |
| cloudmesh_spec.md | What is the RTO and RPO for Tier 1 services and what DR st… | 1 | 0 | 10/10 | Recovery objectives by tier:  | Tier | Service Examples | RTO | RPO | … |
| cloudmesh_spec.md | Which compliance certification covers payment flows and wh… | 3 | 279 | 8/10 | The **PCI DSS Level 1** certification covers the payment‑flow componen… |
| cloudmesh_spec.md | What was the total annual infrastructure cost in FY2024 an… | 1 | 0 | 10/10 | | Category | Annual Cost ($M) | % of Total | YoY Change | |----------|… |
| cloudmesh_spec.md | What is the throughput and latency of the Stream Ingestor … | 1 | 0 | 10/10 | | Component | Technology | Throughput | Latency | Instances | |-------… |
| cloudmesh_spec.md | How many countries does CloudMesh serve and how many regio… | 1 | 0 | 10/10 | CloudMesh is a distributed multi-tenant data platform serving 850+ ent… |
| cloudmesh_spec.md | What is the deployment frequency and mean time to deploy? | 3 | 219 | 9/10 | **Deployment frequency:** ≈ 14.3 deployments per week (average for 202… |
| cloudmesh_spec.md | What is the capacity and replication factor of the Hot Sto… | 1 | 0 | 10/10 | | Store | Technology | Capacity | Replication | Backup Freq | |-------… |
| cloudmesh_spec.md | What is the Tier 0 DR strategy and what services does it c… | 3 | 420 | 10/10 | **Tier 0 Disaster‑Recovery (DR) strategy**  | Aspect | Detail | |-----… |
| cloudmesh_spec.md | What is the SEV-1 incident response time and resolution ta… | 3 | 154 | 10/10 | **SEV‑1 Incident**  - **Response time:** 5 minutes   - **Resolution ta… |
| cloudmesh_spec.md | What is the API rate limit and burst limit for the Platinu… | 3 | 254 | 6/10 | **Platinum tier**  - **API rate limit:** Unlimited calls per minute (n… |
| cloudmesh_spec.md | How many active Prometheus alert rules does CloudMesh have… | 3 | 281 | 6/10 | I’m sorry, but the excerpts you provided don’t contain any information… |
| cloudmesh_spec.md | What is the data retention period for billing records and … | 1 | 0 | 10/10 | | Data Type | Retention Period | Deletion Method | Exceptions | |-----… |
| cloudmesh_spec.md | Which NA-East regional cluster has the lowest latency betw… | 3 | 359 | 9/10 | The sections you’ve shared describe data‑type retention schedules, cla… |

| File | Avg score | Hit-rate (≥7) |
|---|---|---|
| acme_financial.xlsx | 7.8/10 | 80% |
| techvision_annual.docx | 5.9/10 | 57% |
| nexusapi_docs.html | 7.2/10 | 64% |
| cloudmesh_spec.md | 9.1/10 | 87% |
| **Phase 1 overall** | **7.5/10** | **72%** |

_58 questions, 7081 LLM tokens._

## Phase 2 — real PDFs (IPCC · BIS · GPT-3 · Attention · Llama 2 · Constitutional AI)

| File | Question | Layer | Tokens | Score | Answer |
|---|---|---|---|---|---|
| ipcc_spm.pdf | What is the observed increase in global surface temperatur… | 1 | 0 | 9/10 | A.1 Human activities, principally through emissions of greenhouse gase… |
| ipcc_spm.pdf | What is the remaining global carbon budget for limiting wa… | 1 | 0 | 7/10 | Summary for Policymakers B.5.3 If the annual CO2 emissions between 202… |
| ipcc_spm.pdf | What does the report say about limiting warming to 1.5°C —… | 3 | 284 | 5/10 | **Short answer:**   The report says that keeping global warming to the… |
| ipcc_spm.pdf | What are the projected sea level rise ranges mentioned in … | 3 | 231 | 8/10 | **Projected global mean sea‑level rise (relative to the 1995‑2014 base… |
| ipcc_spm.pdf | How does the report link climate change to extreme weather… | 1 | 0 | 0/10 | Summary for Policymakers A.2.4 Climate change has reduced food securit… |
| bis_2024.pdf | What does the BIS 2024 report say about the disinflation p… | 1 | 0 | 9/10 | 4BIS Annual Economic Report 2024 Restricted Inflation receded towards … |
| bis_2024.pdf | What does the BIS 2024 report say about central bank polic… | 1 | 0 | 3/10 | They can thus shed light on the challenges central banks could face in… |
| bis_2024.pdf | What specific financial stability risks does the BIS highl… | 1 | 0 | 3/10 | Even if interest rates return to levels below growth rates, absent con… |
| bis_2024.pdf | What financial stability risks does the BIS identify relat… | 3 | 485 | 3/10 | **Financial‑stability risks highlighted by the BIS**  | Risk | How the… |
| bis_2024.pdf | What fiscal pressure points does the BIS 2024 report ident… | 0 | 0 | 0/10 | Fiscal pressure points: 22 (source: §3.2.1.1.1) |
| gpt3_paper.pdf | What are ALL the parameter sizes of GPT-3 models presented… | 3 | 94 | 5/10 | The sections you supplied ( §24.16, §24.3, §24.5 ) describe the human‑… |
| gpt3_paper.pdf | What are the training corpora (Common Crawl, WebText2, Boo… | 0 | 0 | 0/10 | corpora: 2 (source: §24.3) |
| gpt3_paper.pdf | How does GPT-3's architecture compare to GPT-2? What are t… | 3 | 61 | 0/10 |  |
| gpt3_paper.pdf | How many in-context examples are used in GPT-3's zero-shot… | 1 | 0 | 7/10 | 6 Figure2.1:Zero-shot,one-shotandfew-shot,contrastedwithtraditionalﬁne… |
| gpt3_paper.pdf | What limitations and risks does the GPT-3 paper acknowledg… | 3 | 314 | 9/10 | **Limitations and Risks Highlighted in the GPT‑3 Paper**  | Area | Wha… |
| attention_paper.pdf | How many attention heads does the Transformer base model u… | 3 | 128 | 10/10 | **Answer**  - **Number of attention heads:** **8** per multi‑head‑atte… |
| attention_paper.pdf | What EN-DE BLEU scores do the Transformer base and big mod… | 0 | 0 | 7/10 | Transformer big: 28.441 (source: §24.5) |
| attention_paper.pdf | What training data and hardware were used to train the Tra… | 3 | 237 | 8/10 | **Training data**  * **English → German** – The standard WMT 2014 Engl… |
| attention_paper.pdf | What are the per-layer computational complexities of self-… | 3 | 180 | 10/10 | **Per‑layer computational cost (Table 1)**    | Layer type | Complexit… |
| attention_paper.pdf | What regularization techniques are applied during Transfor… | 3 | 210 | 3/10 | **Regularisation techniques used while training the Transformer**  Acc… |
| llama2_paper.pdf | What are ALL the parameter sizes of the Llama 2 model fami… | 3 | 68 | 0/10 |  |
| llama2_paper.pdf | How does Llama 2-Chat 70B compare to ChatGPT (GPT-3.5) on … | 3 | 247 | 8/10 | **Comparison on Human‑Preference Evaluations**  | Aspect | Llama 2‑Cha… |
| llama2_paper.pdf | What context length do Llama 2 models support, and how was… | 3 | 324 | 10/10 | **Answer**  Llama 2 models can handle **up to 4 k tokens (≈ 4096 token… |
| llama2_paper.pdf | How were the reward models for Llama 2-Chat RLHF trained, … | 1 | 0 | 8/10 | Here,wediscusstheinterestingpropertieswehaveobservedwithRLHF(Section5.… |
| llama2_paper.pdf | What is RLHF Ghost Attention (GAtt) and what problem does … | 2 | 109 | 3/10 | RLHF Ghost Attention (GAtt) is a new technique introduced in Llama 2‑C… |
| constitutional_ai.pdf | What is the core idea behind Constitutional AI and what pr… | 3 | 190 | 5/10 | **Core idea of Constitutional AI (CAI)**    Constitutional AI is a tra… |
| constitutional_ai.pdf | What two training phases make up the Constitutional AI met… | 3 | 48 | 0/10 |  |
| constitutional_ai.pdf | How does the paper evaluate harmlessness vs helpfulness, a… | 3 | 254 | 3/10 | **How the paper measures “harmlessness” versus “helpfulness”**  | Aspe… |
| constitutional_ai.pdf | What is the 'critique and revision' step in Constitutional… | 1 | 0 | 0/10 | 3.1Method WebeginbyshowingthehelpfulRLHFmodelapromptdesignedtoelicitha… |
| constitutional_ai.pdf | What does the Constitutional AI paper show about whether A… | 3 | 282 | 9/10 | **Short answer**  The Constitutional AI (CAI) experiments demonstrate … |

| File | Avg score | Hit-rate (≥7) |
|---|---|---|
| ipcc_spm.pdf | 5.8/10 | 60% |
| bis_2024.pdf | 3.6/10 | 20% |
| gpt3_paper.pdf | 4.2/10 | 40% |
| attention_paper.pdf | 7.6/10 | 80% |
| llama2_paper.pdf | 5.8/10 | 60% |
| constitutional_ai.pdf | 3.4/10 | 20% |
| **Phase 2 overall** | **5.1/10** | **47%** |

_30 questions, 3746 LLM tokens._

## Overall

| Phase | Avg score | Hit-rate (≥7) |
|---|---|---|
| Phase 1 — generated | 7.5/10 | 72% |
| Phase 2 — PDFs | 5.1/10 | 47% |
| **All (88 Qs)** | **6.7/10** | — |

