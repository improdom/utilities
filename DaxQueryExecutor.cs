Hi Venkat,

Could you please confirm the table name from which we should pick up the update? In your update statement you are referencing neu_dev_at40482_mtrc.cubiq_config.cubiq_metadata, but that table does not appear to exist.

I see two other tables:

[cubiq].[cubiq_metadata] – this is the table currently referenced by the MRV Builder.

[config].[cubiq_metadata] – I am not sure how this table is being used.

For our side, the correct table is [cubiq].[cubiq_metadata]. Please confirm that your changes are applied to this table so we can remove the other table if it is not required.

Lokesh, we will also need to include this update in the SQL MI build so it can be deployed to the TEST lane tomorrow.

Thanks,
Julio Diaz
