
Here is an improved version of your email text, maintaining professionalism and clarity:

⸻

Subject: RE: ARC-PBI Strategic Data Model

Hi Sridhar,

I’ve pushed the changes to filter SDC dimensions by CobDate to the following branch:

🔗 Branch Link

⸻

Notes
	•	Modifications were made to the default partitions in the model, aligning them with the in-memory table refresh logic.
	•	A new partition will be created and used for each CobDate.
	•	Perge Service needs to be updated to retain partitions for the last two CobDates.
	•	Consistency is required across all tables for CobDate fields (key name, type, etc.).

View Name	Column	Type
pbi_dim_instrument_vw	business_date	DateTime
pbi_dim_issuer_vw	business_date	DateTime
pbi_dim_le_hierarchy_vw	business_date	DateTime
pbi_fact_ms_bucket_group_map_vw	cob_date	Integer
pbi_fact_issuer_adv_vw	cob_date	Integer
pbi_fact_bucketink_weight_vw	cob_date	Integer
pbi_dim_book_hierarchy_vw	business_date	DateTime

⚠️ In the default partition M query, a dead condition was added to prevent materialization, using it as a template only.

⸻

I used this copy of the model to test the changes: ARCModel_Refresh

Let me know if you have any questions or need further clarification.

Best regards,
Julio

⸻

Let me know if you’d like a more concise or informal version.
