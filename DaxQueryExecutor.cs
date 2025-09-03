Hi Mohan,

I ran the query and did not encounter a timeout error. However, as I suspected, the issue is that the query exceeds the maximum row limit of 1 million rows. To address this, I refactored the DAX to resolve problems related to filters.

Please note that the FILTER function typically generates separate SQL queries where other filters may not be applied, which can significantly increase the row count required to complete the DAX query. Wherever possible, please use KEEPRFILTERS instead of FILTER. Do keep in mind that FILTER and KEEPRFILTERS behave differently depending on the scenario, so it’s important to choose the right one.

Attached is the refactored query for your review.
